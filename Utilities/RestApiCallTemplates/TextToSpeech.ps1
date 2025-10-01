# TextToSpeech.ps1
# Usage: .\TextToSpeech.ps1 -Text "Your text to convert to speech" -OutputFile "output.wav"

param(
    [Parameter(Mandatory = $true)]
    [string]$Text,

    [string]$OutputFile = "speech.wav",

    [string]$ApiUrl = "https://localhost:7176/api/texttospeech/speak"
)

# Prepare the request body
$body = @{
    text = $Text
} | ConvertTo-Json

# Send POST request to the API
try {
    $response = Invoke-WebRequest -Uri $ApiUrl -Method POST -Body $body -ContentType "application/json" -OutFile $OutputFile
    Write-Host "Speech audio saved to $OutputFile"
} catch {
    Write-Error "Failed to get speech audio: $_"
}