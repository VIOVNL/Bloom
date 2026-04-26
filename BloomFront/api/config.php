<?php

define('DATA_DIR', __DIR__ . '/../data');
define('LOG_DIR', __DIR__ . '/../logs');
define('LOG_FILE', LOG_DIR . '/bloom.txt');
define('DB_PATH', DATA_DIR . '/bloom.db');

if (!is_dir(DATA_DIR)) mkdir(DATA_DIR, 0755, true);
if (!is_dir(LOG_DIR)) mkdir(LOG_DIR, 0755, true);

// Block all direct HTTP access to data/logs directories
$denyAll = <<<'HTACCESS'
# Deny all direct HTTP access
<IfModule mod_authz_core.c>
    Require all denied
</IfModule>
<IfModule !mod_authz_core.c>
    Order deny,allow
    Deny from all
</IfModule>
HTACCESS;

foreach ([DATA_DIR, LOG_DIR] as $dir) {
    $htFile = $dir . '/.htaccess';
    if (!is_file($htFile)) {
        file_put_contents($htFile, $denyAll);
    }
}

function bloom_log(string $message): void {
    $message = preg_replace('/[\r\n\x00-\x1F]/', ' ', $message);
    $timestamp = date('Y-m-d H:i:s');
    $line = "[$timestamp] $message" . PHP_EOL;
    file_put_contents(LOG_FILE, $line, FILE_APPEND | LOCK_EX);
}

function get_db(): PDO {
    static $pdo = null;
    if ($pdo === null) {
        $pdo = new PDO('sqlite:' . DB_PATH);
        $pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
        $pdo->exec('PRAGMA journal_mode = WAL');
        $pdo->exec('
            CREATE TABLE IF NOT EXISTS rate_limits (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                rate_key TEXT NOT NULL,
                ts INTEGER NOT NULL
            )
        ');
        $pdo->exec('DELETE FROM rate_limits WHERE ts < ' . (time() - 3600));
    }
    return $pdo;
}

function check_rate_limit(string $action, int $max, int $window): void {
    $ip = $_SERVER['REMOTE_ADDR'] ?? 'unknown';
    $key = $action . ':' . $ip;
    $db = get_db();
    $since = time() - $window;

    $stmt = $db->prepare('SELECT COUNT(*) FROM rate_limits WHERE rate_key = ? AND ts >= ?');
    $stmt->execute([$key, $since]);
    $count = (int) $stmt->fetchColumn();

    $stmt = $db->prepare('INSERT INTO rate_limits (rate_key, ts) VALUES (?, ?)');
    $stmt->execute([$key, time()]);

    if ($count >= $max) {
        bloom_log("RATE LIMIT: $action from $ip ($count attempts)");
        json_response(['error' => 'Too many requests. Try again later.'], 429);
    }
}

function read_json_body(int $maxBytes = 16384): ?array {
    $raw = file_get_contents('php://input', false, null, 0, $maxBytes + 1);
    if (strlen($raw) > $maxBytes) {
        json_response(['error' => 'Request body too large'], 413);
    }
    $data = json_decode($raw, true);
    if (!is_array($data)) {
        json_response(['error' => 'Invalid JSON'], 400);
    }
    return $data;
}

function json_response($data, int $status = 200): void {
    http_response_code($status);
    header('Content-Type: application/json');
    echo json_encode($data);
    exit;
}

function cors_headers(): void {
    header('Access-Control-Allow-Origin: *');
    header('Access-Control-Allow-Methods: POST, OPTIONS');
    header('Access-Control-Allow-Headers: Content-Type');
    if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
        http_response_code(204);
        exit;
    }
}
