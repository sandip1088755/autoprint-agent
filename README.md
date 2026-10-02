# AutoPrint Agent Kit

## Sabse aasan: GitHub se exe banwao (PC pe kuch install nahi)
1. GitHub pe naya PUBLIC repo banao (server/ folder mat daalna).
2. `agent/` folder upload karo.
3. "Add file -> Create new file" me naam likho `.github/workflows/build-agent.yml` aur is kit wali file ka text paste karo.
4. Actions tab -> "Build AutoPrint Agent" -> Run workflow. ~5 minute me artifact `AutoPrintAgent-Setup` (exe) mil jayega.
5. Public link chahiye to tag banao `v1.0.0` -> Releases me exe aa jayega. Wahi link `AGENT_DOWNLOAD_URL` me daalo.

## Server (htdocs)
1. phpMyAdmin me `server/sql/pairing.sql` run karo.
2. server/api/agent/{pair,next-job,claim,download}.php aur server/shop/agent-setup.php upload karo.
3. config.php me: define('AGENT_DOWNLOAD_URL', 'https://github.com/<user>/<repo>/releases/latest/download/AutoPrintAgent-Setup.exe');

## Flow (shopkeeper ke liye)
1. Shop panel -> Print Agent -> "Config File Download Karo" (autoprint-config.json, Downloads me rahe).
2. AutoPrintAgent-Setup.exe download karke chalao.
3. App khulte hi code apne aap bhar jata hai -> printer chuno -> Connect. Bas.
(Config file ke bina bhi chalega: "Naya Pairing Code" banao aur haath se daalo.)
