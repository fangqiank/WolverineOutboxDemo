import { invoke } from "@tauri-apps/api/core";

interface UserDto {
  id: string;
  email: string;
  name: string;
  status: string;
  createdAt: string;
  completedAt: string | null;
}

interface RegisterResponse {
  userId: string;
  message: string;
}

interface MessageHistoryDto {
  id: string;
  correlationId: string;
  messageType: string;
  direction: string;
  description: string;
  timestamp: string;
}

let userListEl: HTMLElement | null;
let statusEl: HTMLElement | null;
let historyEl: HTMLElement | null;

async function loadUsers() {
  if (!userListEl) return;
  try {
    const users = await invoke<UserDto[]>("list_users");
    if (users.length === 0) {
      userListEl.innerHTML = '<tr><td colspan="6" class="empty">No users yet</td></tr>';
      return;
    }
    userListEl.innerHTML = users
      .map(
        (u) => `<tr>
          <td title="${u.id}">${u.id.slice(0, 8)}...</td>
          <td>${escapeHtml(u.email)}</td>
          <td>${escapeHtml(u.name)}</td>
          <td><span class="badge">${escapeHtml(u.status)}</span></td>
          <td>${formatDate(u.createdAt)}</td>
          <td>${u.completedAt ? formatDate(u.completedAt) : "-"}</td>
        </tr>`
      )
      .join("");
  } catch (e) {
    userListEl.innerHTML = `<tr><td colspan="6" class="error">Failed to load: ${e}</td></tr>`;
  }
}

async function registerUser(mode: "outbox" | "unsafe") {
  const emailInput = document.querySelector<HTMLInputElement>("#reg-email");
  const nameInput = document.querySelector<HTMLInputElement>("#reg-name");
  if (!emailInput || !nameInput || !statusEl) return;

  const email = emailInput.value.trim();
  const name = nameInput.value.trim();

  if (!email || !name) {
    setStatus("Please fill in all fields", "error");
    return;
  }

  const cmd = mode === "outbox" ? "register_user_outbox" : "register_user_unsafe";
  try {
    const result = await invoke<RegisterResponse>(cmd, { email, name });
    const id = result.userId || "unknown";
    setStatus(`${mode.toUpperCase()}: ${result.message} (ID: ${id.slice(0, 8)}...)`, "success");
    emailInput.value = "";
    nameInput.value = "";
    await loadUsers();
    await loadHistory();
  } catch (e) {
    setStatus(`Error: ${e}`, "error");
  }
}

function directionLabel(dir: string): string {
  switch (dir) {
    case "Sent": return "badge-sent";
    case "Received": return "badge-received";
    case "Completed": return "badge-completed";
    default: return "badge";
  }
}

function directionIcon(dir: string): string {
  switch (dir) {
    case "Sent": return "&#8594;";       // →
    case "Received": return "&#8592;";   // ←
    case "Completed": return "&#10003;"; // ✓
    default: return "&#8226;";           // •
  }
}

async function loadHistory() {
  if (!historyEl) return;
  try {
    const history = await invoke<MessageHistoryDto[]>("list_message_history");
    if (history.length === 0) {
      historyEl.innerHTML = '<tr><td colspan="5" class="empty">No message flow events yet</td></tr>';
      return;
    }
    historyEl.innerHTML = history
      .map((h) => {
        const badgeClass = directionLabel(h.direction);
        const icon = directionIcon(h.direction);
        return `<tr>
          <td class="mono">${formatDate(h.timestamp)}</td>
          <td title="${h.correlationId}" class="mono">${h.correlationId.slice(0, 8)}...</td>
          <td>${escapeHtml(h.messageType)}</td>
          <td><span class="badge ${badgeClass}">${icon} ${escapeHtml(h.direction)}</span></td>
          <td>${escapeHtml(h.description)}</td>
        </tr>`;
      })
      .join("");
  } catch (e) {
    historyEl.innerHTML = `<tr><td colspan="5" class="error">Failed to load: ${e}</td></tr>`;
  }
}

function setStatus(msg: string, type: "success" | "error") {
  if (!statusEl) return;
  statusEl.textContent = msg;
  statusEl.className = `status${type === "error" ? " status-error" : ""}`;
  if (type === "success") {
    setTimeout(() => { if (statusEl) statusEl.className = "status"; }, 3000);
  }
}

function escapeHtml(s: string): string {
  const d = document.createElement("div");
  d.textContent = s;
  return d.innerHTML;
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

window.addEventListener("DOMContentLoaded", () => {
  userListEl = document.querySelector("#user-list");
  statusEl = document.querySelector("#status-msg");
  historyEl = document.querySelector("#history-list");

  document.querySelector("#btn-outbox")?.addEventListener("click", () => registerUser("outbox"));
  document.querySelector("#btn-unsafe")?.addEventListener("click", () => registerUser("unsafe"));
  document.querySelector("#btn-refresh")?.addEventListener("click", () => {
    loadUsers();
    loadHistory();
  });

  loadUsers();
  loadHistory();
  setInterval(() => {
    loadUsers();
    loadHistory();
  }, 5000);
});
