# JWT Bearer Authentication Guide

This document describes how to use JWT Bearer authentication to access the protected API endpoints in the Fashion Assistant application.

## Overview

The application uses JWT (JSON Web Token) Bearer authentication to secure API endpoints. All `/api/cart` and `/api/inventory` endpoints require a valid JWT token to access.

## Getting Started

### 1. Generate a JWT Token

To access protected endpoints, you first need to obtain a JWT token by calling the authentication endpoint:

**Endpoint**: `POST /api/auth/token`

**Request Body**:
```json
{
  "Username": "testuser"
}
```

#### PowerShell Example:
```powershell
$body = @{ Username = "testuser" } | ConvertTo-Json
$response = Invoke-RestMethod -Uri "https://app-web-5bvhvhsyujn6y.azurewebsites.net/api/auth/token" -Method Post -Body $body -ContentType "application/json"
$token = $response.token
```

#### cURL Example:
```bash
curl -X POST "https://app-web-5bvhvhsyujn6y.azurewebsites.net/api/auth/token" \
  -H "Content-Type: application/json" \
  -d '{"Username":"testuser"}'
```

**Response**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-10-14T03:11:58Z",
  "username": "testuser"
}
```

### 2. Use the Token to Access Protected Endpoints

Include the token in the `Authorization` header with the `Bearer` scheme:

```
Authorization: Bearer <your-token-here>
```

## API Examples

### Get Inventory (Without Authentication) ❌

Attempting to access a protected endpoint without authentication will result in a `401 Unauthorized` error:

```powershell
# This will fail with 401 Unauthorized
try {
    Invoke-RestMethod -Uri "https://app-web-5bvhvhsyujn6y.azurewebsites.net/api/inventory" -Method Get
} catch {
    Write-Host "Error: $($_.Exception.Response.StatusCode)"
}
```

**Response**: `401 Unauthorized`

### Get Inventory (With Authentication) ✅

```powershell
# Set up the authorization header
$headers = @{ Authorization = "Bearer $token" }

# Call the protected endpoint
$inventory = Invoke-RestMethod -Uri "https://app-web-5bvhvhsyujn6y.azurewebsites.net/api/inventory" -Method Get -Headers $headers

# Display results
$inventory | Format-Table productId, name, price
```

**Response**:
```json
[
  {
    "productId": 3,
    "name": "Product Name",
    "price": 89.99,
    "sizeInventory": {
      "S": 10,
      "M": 15,
      "L": 8
    }
  }
]
```

### Get Shopping Cart

```powershell
$cart = Invoke-RestMethod -Uri "https://app-web-5bvhvhsyujn6y.azurewebsites.net/api/cart" -Method Get -Headers $headers
$cart | ConvertTo-Json
```

**Response**:
```json
{
  "items": [],
  "totalCost": 0
}
```

### Add Item to Cart

```powershell
$addToCartBody = @{
    ProductId = 3
    Size = "M"
    Quantity = 2
} | ConvertTo-Json

$cart = Invoke-RestMethod -Uri "https://app-web-5bvhvhsyujn6y.azurewebsites.net/api/cart/add" -Method Post -Body $addToCartBody -ContentType "application/json" -Headers $headers
$cart | ConvertTo-Json
```

### Get Specific Inventory Item

```powershell
$item = Invoke-RestMethod -Uri "https://app-web-5bvhvhsyujn6y.azurewebsites.net/api/inventory/3" -Method Get -Headers $headers
$item | ConvertTo-Json
```

### Check Size Availability

```powershell
$sizeInfo = Invoke-RestMethod -Uri "https://app-web-5bvhvhsyujn6y.azurewebsites.net/api/inventory/3/size/M" -Method Get -Headers $headers
$sizeInfo | ConvertTo-Json
```

## Complete PowerShell Workflow

Here's a complete example workflow from token generation to API usage:

```powershell
# 1. Generate JWT token
$body = @{ Username = "testuser" } | ConvertTo-Json
$response = Invoke-RestMethod -Uri "https://app-web-5bvhvhsyujn6y.azurewebsites.net/api/auth/token" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"

# 2. Extract token and set up headers
$token = $response.token
$headers = @{ Authorization = "Bearer $token" }

Write-Host "Token obtained. Expires at: $($response.expiresAt)"

# 3. Get inventory
$inventory = Invoke-RestMethod -Uri "https://app-web-5bvhvhsyujn6y.azurewebsites.net/api/inventory" `
    -Method Get `
    -Headers $headers

Write-Host "`nInventory Items: $($inventory.Count)"
$inventory | Format-Table productId, name, price

# 4. Get cart
$cart = Invoke-RestMethod -Uri "https://app-web-5bvhvhsyujn6y.azurewebsites.net/api/cart" `
    -Method Get `
    -Headers $headers

Write-Host "`nCart Total: $($cart.totalCost)"
```

## Using Swagger UI

The application includes Swagger UI with built-in JWT authentication support:

1. Navigate to `/swagger` in your browser
2. Click the **Authorize** button (🔒 icon)
3. Enter your token in the format: `Bearer <your-token>`
4. Click **Authorize**
5. All subsequent API calls will include the authentication header

## Token Details

- **Algorithm**: HS256 (HMAC with SHA-256)
- **Expiration**: 60 minutes (configurable)
- **Claims**:
  - `sub`: Username
  - `jti`: Unique token identifier
  - `name`: Username

## Configuration

JWT settings are configured in `appsettings.json`:

```json
{
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLongForHS256!",
    "Issuer": "FashionStoreAPI",
    "Audience": "FashionStoreClients",
    "ExpirationMinutes": 60
  }
}
```

> ⚠️ **Security Note**: In production, the `SecretKey` should be stored securely (e.g., Azure Key Vault) and not committed to source control.

## Protected Endpoints

The following endpoints require JWT authentication:

### Cart Controller (`/api/cart`)
- `GET /api/cart` - Get cart contents
- `POST /api/cart/add` - Add item to cart
- `PUT /api/cart/{productId}/size/{size}` - Update cart item quantity
- `DELETE /api/cart/{productId}/size/{size}` - Remove item from cart

### Inventory Controller (`/api/inventory`)
- `GET /api/inventory` - Get all inventory items
- `GET /api/inventory/{id}` - Get specific inventory item
- `GET /api/inventory/{id}/size/{size}` - Get size-specific inventory
- `GET /api/inventory/sizes` - Get available sizes

## Troubleshooting

### 401 Unauthorized Error
- Verify the token is included in the `Authorization` header
- Check that the header format is: `Bearer <token>`
- Ensure the token hasn't expired (60-minute default)
- Generate a new token if needed

### 400 Bad Request on Token Generation
- Verify the request body is valid JSON
- Ensure `Username` field is provided and not empty
- Check `Content-Type` header is set to `application/json`

## Testing Results

✅ **Token Generation**: Successfully generates JWT tokens  
✅ **Unauthorized Access**: Returns 401 without token  
✅ **Authenticated Access**: Successfully accesses protected endpoints with valid token  
✅ **Token Expiration**: Tokens expire after configured duration (60 minutes)

---

*Last Updated: October 14, 2025*
