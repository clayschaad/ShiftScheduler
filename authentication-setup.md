# Authentication Setup Guide

## Overview
The ShiftScheduler application now includes Google OAuth authentication with configurable authorized email addresses. Only users with emails listed in the configuration can access the application.

## Setup Instructions

### 1. Create Google OAuth Application
1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the Google+ API
4. Go to "Credentials" and create OAuth 2.0 Client IDs
5. Set the authorized redirect URI to: `http://localhost:5000/signin-google` (for development)
6. For production, use your domain: `https://yourdomain.com/signin-google`

### 2. Configure Application
Edit `Server/appsettings.json` and update the authentication section:

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "your-google-client-id.apps.googleusercontent.com",
      "ClientSecret": "your-google-client-secret"
    },
    "AuthorizedEmails": [
      "user1@gmail.com",
      "user2@example.com"
    ]
  }
}
```

### 3. For Production
For production deployment, consider using environment variables or Azure Key Vault:
- `Authentication__Google__ClientId`
- `Authentication__Google__ClientSecret`
- `Authentication__AuthorizedEmails__0`, `Authentication__AuthorizedEmails__1`, etc.

## How It Works

### Authentication Flow
1. Unauthenticated users see a login screen
2. Clicking "Sign in with Google" redirects to Google OAuth
3. After successful Google authentication, the application checks if the user's email is in the authorized list
4. Authorized users are redirected to the main application
5. Unauthorized users are redirected back with an error message

### API Security
- All API endpoints require authentication (`[Authorize]` attribute)
- Only users with emails in the `AuthorizedEmails` list can access the API
- Unauthenticated requests return a 302 redirect to login

### User Interface
- **Login Screen**: Clean, centered login form with Google sign-in button
- **Authenticated Header**: Shows user email and sign-out button
- **Main Application**: Normal shift scheduler functionality for authenticated users

## Testing
To test the authentication:
1. Configure Google OAuth credentials as described above
2. Add your email to the `AuthorizedEmails` list
3. Start the application: `dotnet run` from the Server directory
4. Navigate to `http://localhost:5000`
5. Click "Sign in with Google" and complete the OAuth flow

## Security Features
- **Email-based Authorization**: Only specified emails can access the application
- **Secure API Endpoints**: All shift management APIs require authentication
- **Session Management**: Proper login/logout functionality
- **OAuth Integration**: Uses Google's secure OAuth 2.0 flow