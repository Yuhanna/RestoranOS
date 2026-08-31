import { createRoot } from "react-dom/client";
import { App } from "./App";
import { bootstrapTheme } from "./theme";
import "./styles.css";

bootstrapTheme();

const root = document.getElementById("root");
if (!root) throw new Error("Application root was not found.");
createRoot(root).render(<App />);
