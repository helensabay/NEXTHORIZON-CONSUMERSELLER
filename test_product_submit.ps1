# Test script to submit a product with color variants
$url = "http://localhost:5297/Seller/CreateProduct"
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

# First, get the CSRF token
$response = Invoke-WebRequest -Uri "http://localhost:5297/Seller/CreateProduct" -WebSession $session
$html = $response.Content
$tokenMatch = [regex]::Match($html, '<input[^>]*name="__RequestVerificationToken"[^>]*value="([^"]*)"')
$csrfToken = $tokenMatch.Groups[1].Value

Write-Host "CSRF Token: $csrfToken"

# Prepare form data
$form = @{
    "__RequestVerificationToken" = $csrfToken
    "ProductName" = "Test Product $(Get-Random)"
    "Price" = "100"
    "Discount" = "10"
    "Category" = "Men"
    "Gender" = "Men"
    "Details" = "Test product details"
    "Brand" = "TestBrand"
    "Stock" = "100"
    "Status" = "active"
    "colorNames" = @("Red", "Blue")
    "colorStocks" = @("50", "50")
    "colorSizes" = @("S,M,L", "M,L,XL")
    "mode" = "create"
}

# Submit the form
try {
    $result = Invoke-WebRequest -Uri $url -Method Post -WebSession $session -Body $form
    Write-Host "Status Code: $($result.StatusCode)"
    Write-Host "Response URL: $($result.BaseResponse.RequestMessage.RequestUri)"
    if ($result.BaseResponse.RequestMessage.RequestUri -match "Error") {
        Write-Host "ERROR PAGE DETECTED"
        # Extract error from response
        if ($result.Content -match '<pre>(.*?)</pre>') {
            $error = $matches[1]
            Write-Host "Error Details: $error"
        }
    }
} catch {
    Write-Host "Exception: $_"
    Write-Host "Response: $($_.Exception.Response)"
}
