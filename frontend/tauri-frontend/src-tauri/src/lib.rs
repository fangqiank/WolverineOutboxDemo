use serde::{Deserialize, Serialize};

const API_BASE: &str = "http://localhost:5143";
const RABBIT_API: &str = "http://localhost:15672";

#[derive(Serialize, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct UserDto {
    pub id: String,
    pub email: String,
    pub name: String,
    pub status: String,
    pub created_at: String,
    pub completed_at: Option<String>,
}

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegisterRequest {
    pub email: String,
    pub name: String,
}

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegisterResponse {
    pub user_id: String,
    pub message: String,
}

#[tauri::command]
async fn register_user_outbox(email: String, name: String) -> Result<RegisterResponse, String> {
    let client = reqwest::Client::new();
    client
        .post(format!("{API_BASE}/api/users/register-outbox"))
        .json(&RegisterRequest { email, name })
        .send()
        .await
        .map_err(|e| e.to_string())?
        .json::<RegisterResponse>()
        .await
        .map_err(|e| e.to_string())
}

#[tauri::command]
async fn register_user_unsafe(email: String, name: String) -> Result<RegisterResponse, String> {
    let client = reqwest::Client::new();
    client
        .post(format!("{API_BASE}/api/users/register-unsafe"))
        .json(&RegisterRequest { email, name })
        .send()
        .await
        .map_err(|e| e.to_string())?
        .json::<RegisterResponse>()
        .await
        .map_err(|e| e.to_string())
}

#[tauri::command]
async fn list_users() -> Result<Vec<UserDto>, String> {
    let client = reqwest::Client::new();
    client
        .get(format!("{API_BASE}/api/users"))
        .send()
        .await
        .map_err(|e| e.to_string())?
        .json::<Vec<UserDto>>()
        .await
        .map_err(|e| e.to_string())
}

#[tauri::command]
async fn get_user(user_id: String) -> Result<Option<UserDto>, String> {
    let client = reqwest::Client::new();
    let resp = client
        .get(format!("{API_BASE}/api/users/{user_id}"))
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if resp.status().is_success() {
        resp.json::<UserDto>().await.map(Some).map_err(|e| e.to_string())
    } else {
        Ok(None)
    }
}

#[derive(Serialize, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct EnvelopeInfo {
    pub id: String,
    pub destination: String,
    pub message_type: String,
    pub attempts: i64,
    pub deliver_by: Option<String>,
}

#[tauri::command]
async fn list_outbox() -> Result<Vec<EnvelopeInfo>, String> {
    let client = reqwest::Client::new();
    client
        .get(format!("{API_BASE}/api/outbox"))
        .send()
        .await
        .map_err(|e| e.to_string())?
        .json::<Vec<EnvelopeInfo>>()
        .await
        .map_err(|e| e.to_string())
}

#[tauri::command]
async fn list_inbox() -> Result<Vec<EnvelopeInfo>, String> {
    let client = reqwest::Client::new();
    client
        .get(format!("{API_BASE}/api/inbox"))
        .send()
        .await
        .map_err(|e| e.to_string())?
        .json::<Vec<EnvelopeInfo>>()
        .await
        .map_err(|e| e.to_string())
}

#[derive(Serialize, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct MessageHistoryDto {
    pub id: String,
    pub correlation_id: String,
    pub message_type: String,
    pub direction: String,
    pub description: String,
    pub timestamp: String,
}

#[tauri::command]
async fn list_message_history() -> Result<Vec<MessageHistoryDto>, String> {
    let client = reqwest::Client::new();
    client
        .get(format!("{API_BASE}/api/message-history"))
        .send()
        .await
        .map_err(|e| e.to_string())?
        .json::<Vec<MessageHistoryDto>>()
        .await
        .map_err(|e| e.to_string())
}

#[derive(Serialize, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct SagaDto {
    pub id: String,
    pub email: String,
    pub name: String,
    pub status: String,
    pub started_at: String,
    pub completed_at: Option<String>,
}

#[tauri::command]
async fn list_sagas() -> Result<Vec<SagaDto>, String> {
    let client = reqwest::Client::new();
    client
        .get(format!("{API_BASE}/api/sagas"))
        .send()
        .await
        .map_err(|e| e.to_string())?
        .json::<Vec<SagaDto>>()
        .await
        .map_err(|e| e.to_string())
}

#[derive(Serialize, Deserialize, Clone)]
pub struct RabbitQueue {
    pub name: String,
    pub messages: i64,
    pub messages_ready: i64,
    pub messages_unacknowledged: i64,
    pub consumers: i64,
    pub state: Option<String>,
}

#[derive(Serialize, Deserialize, Clone)]
pub struct RabbitExchange {
    pub name: String,
    #[serde(rename = "type")]
    pub exchange_type: String,
    pub message_stats: Option<RabbitExchangeStats>,
}

#[derive(Serialize, Deserialize, Clone)]
pub struct RabbitExchangeStats {
    pub publish_in: Option<i64>,
    pub publish_in_details: Option<RabbitRate>,
    pub publish_out: Option<i64>,
    pub publish_out_details: Option<RabbitRate>,
}

#[derive(Serialize, Deserialize, Clone)]
pub struct RabbitRate {
    pub rate: f64,
}

#[derive(Serialize, Deserialize, Clone)]
pub struct RabbitOverview {
    pub rabbitmq_version: String,
    pub management_version: String,
    pub cluster_name: String,
    pub queue_totals: RabbitQueueTotals,
    pub message_stats: Option<RabbitMessageStats>,
}

#[derive(Serialize, Deserialize, Clone)]
pub struct RabbitQueueTotals {
    pub messages: i64,
    pub messages_ready: i64,
    pub messages_unacknowledged: i64,
}

#[derive(Serialize, Deserialize, Clone)]
pub struct RabbitMessageStats {
    pub publish_details: RabbitRate,
    pub deliver_get_details: RabbitRate,
}

#[derive(Serialize, Deserialize, Clone)]
pub struct RabbitInfo {
    pub overview: RabbitOverview,
    pub queues: Vec<RabbitQueue>,
    pub exchanges: Vec<RabbitExchange>,
}

#[tauri::command]
async fn get_rabbit_info() -> Result<RabbitInfo, String> {
    let client = reqwest::Client::new();

    let overview = client
        .get(format!("{RABBIT_API}/api/overview"))
        .basic_auth("guest", Some("guest"))
        .send()
        .await
        .map_err(|e| e.to_string())?
        .json::<RabbitOverview>()
        .await
        .map_err(|e| e.to_string())?;

    let queues = client
        .get(format!("{RABBIT_API}/api/queues"))
        .basic_auth("guest", Some("guest"))
        .send()
        .await
        .map_err(|e| e.to_string())?
        .json::<Vec<RabbitQueue>>()
        .await
        .map_err(|e| e.to_string())?;

    let exchanges: Vec<RabbitExchange> = client
        .get(format!("{RABBIT_API}/api/exchanges"))
        .basic_auth("guest", Some("guest"))
        .send()
        .await
        .map_err(|e| e.to_string())?
        .json::<Vec<RabbitExchange>>()
        .await
        .map_err(|e| e.to_string())?
        .into_iter()
        .filter(|e| !e.name.is_empty())
        .collect();

    Ok(RabbitInfo { overview, queues, exchanges })
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .invoke_handler(tauri::generate_handler![
            register_user_outbox,
            register_user_unsafe,
            list_users,
            get_user,
            list_outbox,
            list_inbox,
            list_message_history,
            list_sagas,
            get_rabbit_info,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
