Stop-Process -Name "Paws.Sidecar" -Force -ErrorAction SilentlyContinue

dotnet publish src-backend/Paws.Sidecar/Paws.Sidecar.csproj `
    -r win-x64 `
    -c Release `
    -p:OsuClientId="41" `
    -p:OsuBaseAuthUrl="https://dev.ppy.sh" `
    # \/ Your worker URL and oauth callback, recommended for securing the clientsecret `
    -p:OsuProxyUrl="YOUR_PROXY_URL" `
    -p:OsuRedirectUrl="YOUR_PROXY_URL/callback" `
    # \/ Leave empty for prod; if not empty, overrides proxy (don't fill for release!) `
    -p:OsuClientSecret="" `
    --self-contained true
