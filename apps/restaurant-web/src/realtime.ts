import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import { api, type ManagementApi } from "./api";
import type { Order, ServiceRequest, AudienceNotification } from "./domain";

export type RealtimeState = "connecting" | "connected" | "reconnecting" | "offline";

export type LivePanelDenied = {
  message?: string;
};

export type RealtimeCallbacks = {
  onOrder: (order: Order) => void;
  onResync: (orders: Order[]) => void;
  onState: (state: RealtimeState) => void;
  onServiceRequest?: (request: ServiceRequest) => void;
  onAudienceNotification?: (notification: AudienceNotification) => void;
  onLivePanelDenied?: (payload: LivePanelDenied) => void;
};

export type RealtimeClient = {
  start(callbacks: RealtimeCallbacks): Promise<() => Promise<void>>;
};

export class SignalRRealtimeClient implements RealtimeClient {
  constructor(
    private readonly baseUrl: string,
    private readonly managementApi: ManagementApi,
    private readonly buildConnection: (url: string, token: () => string) => HubConnection = (
      url,
      token,
    ) =>
      new HubConnectionBuilder()
        .withUrl(url, { accessTokenFactory: token })
        .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
        .configureLogging(LogLevel.Warning)
        .build(),
  ) {}

  async start(callbacks: RealtimeCallbacks): Promise<() => Promise<void>> {
    callbacks.onState("connecting");
    const connection = this.buildConnection(
      `${this.baseUrl.replace(/\/+$/, "")}/hubs/v1/management-orders`,
      () => this.managementApi.getSession()?.accessToken ?? "",
    );
    connection.on("orderStatusChanged", callbacks.onOrder);
    if (callbacks.onServiceRequest) {
      connection.on("serviceRequestCreated", callbacks.onServiceRequest);
    }
    if (callbacks.onAudienceNotification) {
      connection.on("audienceNotificationPublished", callbacks.onAudienceNotification);
    }
    if (callbacks.onLivePanelDenied) {
      connection.on("livePanelDenied", callbacks.onLivePanelDenied);
    }
    connection.onreconnecting(async () => {
      callbacks.onState("reconnecting");
      try {
        await this.managementApi.restore();
      } catch {
        callbacks.onState("offline");
      }
    });
    connection.onreconnected(async () => {
      callbacks.onState("connected");
      try {
        callbacks.onResync(await this.managementApi.getActiveOrders());
      } catch {
        callbacks.onState("offline");
      }
    });
    connection.onclose(() => callbacks.onState("offline"));
    try {
      await connection.start();
      callbacks.onState("connected");
    } catch (error) {
      callbacks.onState("offline");
      await connection.stop();
      throw error;
    }

    return async () => {
      connection.off("orderStatusChanged", callbacks.onOrder);
      if (callbacks.onServiceRequest) {
        connection.off("serviceRequestCreated", callbacks.onServiceRequest);
      }
      if (callbacks.onAudienceNotification) {
        connection.off("audienceNotificationPublished", callbacks.onAudienceNotification);
      }
      if (callbacks.onLivePanelDenied) {
        connection.off("livePanelDenied", callbacks.onLivePanelDenied);
      }
      if (connection.state !== HubConnectionState.Disconnected) await connection.stop();
    };
  }
}

export const realtime = new SignalRRealtimeClient(
  import.meta.env.VITE_MANAGEMENT_API_BASE_URL ?? "",
  api,
);
