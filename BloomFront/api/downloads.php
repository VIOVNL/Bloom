<?php
require __DIR__ . '/config.php';
cors_headers();

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    json_response(['error' => 'Method not allowed'], 405);
}

// 30 download tracks per minute per IP
check_rate_limit('download', 30, 60);

$db = get_db();
$db->exec('
    CREATE TABLE IF NOT EXISTS downloads (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        source TEXT NOT NULL DEFAULT \'landing\',
        created_at TEXT NOT NULL DEFAULT (datetime(\'now\'))
    )
');

$input = read_json_body(1024);
$source = $input['source'] ?? 'landing';

// Only allow known source values
$allowedSources = ['landing', 'petal', 'changelog', 'direct'];
if (!in_array($source, $allowedSources)) {
    $source = 'other';
}

$stmt = $db->prepare('INSERT INTO downloads (source) VALUES (?)');
$stmt->execute([$source]);

json_response(['ok' => true]);
