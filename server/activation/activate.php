<?php
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

function answer(bool $valid, string $message, int $status = 200): never {
    http_response_code($status);
    echo json_encode(['valid' => $valid, 'message' => $message], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}
function b64url(string $value): string|false {
    $value = strtr($value, '-_', '+/');
    return base64_decode($value . str_repeat('=', (4 - strlen($value) % 4) % 4), true);
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') answer(false, 'Method not allowed', 405);
$input = json_decode((string)file_get_contents('php://input'), true);
$code = trim((string)($input['code'] ?? ''));
$installation = strtoupper(trim((string)($input['installationCode'] ?? '')));
if ($code === '' || $installation === '') answer(false, 'Missing activation data', 400);
$parts = explode('.', $code);
if (count($parts) !== 3 || $parts[0] !== 'SP1') answer(false, 'Invalid key');
$payload = b64url($parts[1]);
$signature = b64url($parts[2]);
if ($payload === false || $signature === false || $payload !== 'StreamPAL|' . $installation) answer(false, 'Invalid key');

$publicKey = <<<'PEM'
-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEbUv366DtwI0lTeQYrfsX9oXA98r8
dYgFlhvABI43NE/6WM4mC7qowj/EWWuAu/ya17Et+fNWhOAQU/+BE9wmAA==
-----END PUBLIC KEY-----
PEM;
if (openssl_verify($payload, $signature, $publicKey, OPENSSL_ALGO_SHA256) !== 1) answer(false, 'Invalid key');

$dataDir = __DIR__ . '/data';
if (!is_dir($dataDir) && !mkdir($dataDir, 0700, true)) answer(false, 'Activation database unavailable', 503);
$database = $dataDir . '/activations.json';
$handle = fopen($database, 'c+');
if ($handle === false || !flock($handle, LOCK_EX)) answer(false, 'Activation database unavailable', 503);
$contents = stream_get_contents($handle);
$records = $contents ? json_decode($contents, true) : [];
if (!is_array($records)) $records = [];
$keyHash = hash('sha256', $code);
if (isset($records[$keyHash]) && !hash_equals((string)$records[$keyHash]['installationCode'], $installation)) {
    flock($handle, LOCK_UN); fclose($handle); answer(false, 'Key already used on another computer');
}
$records[$keyHash] = ['installationCode' => $installation, 'activatedAt' => gmdate('c')];
rewind($handle); ftruncate($handle, 0); fwrite($handle, json_encode($records, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES)); fflush($handle); flock($handle, LOCK_UN); fclose($handle);
answer(true, 'Activation completed');
