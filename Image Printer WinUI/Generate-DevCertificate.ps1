# Generates a local code-signing cert (CN=jrsco) for SIDELOAD only.
# Prefer: Debug with launch profile "Image Printer WinUI (Unpackaged)" (no cert needed).
#
# DO NOT use this cert when creating Microsoft Store (.msixupload) packages.
# Signing as CN=jrsco rewrites Identity Publisher/PFN and Partner Center rejects the upload.
# Store builds must stay unsigned (AppxPackageSigningEnabled=false).
# Sideload with this cert: msbuild -p:EnableSideloadSigning=true (and thumbprint below).
$ErrorActionPreference = "Stop"
$projectDir = $PSScriptRoot
$pfxPath = Join-Path $projectDir "ImagePrinter_TemporaryKey.pfx"
$cerPath = Join-Path $projectDir "ImagePrinter_TemporaryKey.cer"
$userPath = Join-Path $projectDir "Image Printer WinUI.csproj.user"
$passwordText = "ImagePrinterDev!"

Get-ChildItem Cert:\CurrentUser\My |
	Where-Object { $_.FriendlyName -eq "Image Printer Temporary" } |
	ForEach-Object { Remove-Item $_.PSPath -Force }

$cert = New-SelfSignedCertificate `
	-Type Custom `
	-Subject "CN=jrsco" `
	-KeyUsage DigitalSignature `
	-FriendlyName "Image Printer Temporary" `
	-CertStoreLocation "Cert:\CurrentUser\My" `
	-TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$pwd = ConvertTo-SecureString -String $passwordText -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pwd | Out-Null
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

certutil -user -addstore Root $cerPath | Out-Null
certutil -user -addstore TrustedPeople $cerPath | Out-Null
certutil -user -addstore TrustedPublisher $cerPath | Out-Null

# AppX sideload requires Local Machine Trusted People (needs elevation)
$machineResult = certutil -addstore TrustedPeople $cerPath 2>&1
if ($LASTEXITCODE -ne 0) {
	Write-Warning "Could not add cert to Local Machine\TrustedPeople (run this script elevated for Package deploy)."
	Write-Warning "$machineResult"
}
else {
	Write-Host "Trusted cert in Local Machine\TrustedPeople"
}

$thumb = $cert.Thumbprint
Write-Host "Created $pfxPath"
Write-Host "Thumbprint: $thumb"

if (Test-Path $userPath) {
	$content = Get-Content $userPath -Raw
	if ($content -match "<PackageCertificateThumbprint>[^<]*</PackageCertificateThumbprint>") {
		$content = $content -replace "<PackageCertificateThumbprint>[^<]*</PackageCertificateThumbprint>", "<PackageCertificateThumbprint>$thumb</PackageCertificateThumbprint>"
	}
	else {
		$content = $content -replace "(<PropertyGroup>\r?\n)", "`$1    <PackageCertificateThumbprint>$thumb</PackageCertificateThumbprint>`r`n"
	}
	Set-Content -Path $userPath -Value $content -NoNewline
	Write-Host "Updated PackageCertificateThumbprint in csproj.user"
}
else {
	Write-Host "Add this to Image Printer WinUI.csproj.user inside a PropertyGroup:"
	Write-Host "  <PackageCertificateThumbprint>$thumb</PackageCertificateThumbprint>"
}

Write-Host ""
Write-Host "Default debug profile is Unpackaged (no MSIX). For Package profile, use a trusted Local Machine cert then F5."
