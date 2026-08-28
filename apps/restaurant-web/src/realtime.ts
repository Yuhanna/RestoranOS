import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import { api, type ManagementApi } from "./api";
import type { Order } from "./domain";

export type RealtimeState = "connecting" | "connected" | "reconnecting" | "offline";

export type RealtimeClient = {
  start(
    onOrder: (order: Order) => void,
    onResync: (orders: Order[]) => void,
    onState: (state: RealtimeState) => void,
  ): Promise<() => Promise<void>>;
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

  async start(
    onOrder: (order: Order) => void,
    onResync: (orders: Order[]) => void,
    onState: (state: RealtimeState) => void,
  ): Promise<() => Promise<void>> {
    onState("connecting");
    const connection = this.buildConnection(
      `${this.baseUrl.replace(/\/+$/, "")}/hubs/v1/management-orders`,
      () => this.managementApi.getSession()?.accessToken ?? "",
    );
    connection.on("orderStatusChanged", onOrder);
    connection.onreconnecting(async () => {
      onState("reconnecting");
      try {
        await this.managementApi.restore();
      } catch {
        onState("offline");
      }
    });
    connection.onreconnected(async () => {
      onState("connected");
      try {
        onResync(await this.managementApi.getActiveOrders());
      } catch {
        onState("offline");
      }
    });
    connection.onclose(() => onState("offline"));
    try {
      await connection.start();
      onState("connected");
    } catch (error) {
      onState("offline");
      await connection.stop();
      throw error;
    }

    return async () => {
      connection.off("orderStatusChanged", onOrder);
      if (connection.state !== HubConnectionState.Disconnected) await connection.stop();
    };
  }
}

export const realtime = new SignalRRealtimeClient(
  import.meta.env.VITE_MANAGEMENT_API_BASE_URL ?? "",
  api,
);
