# Azure OpenAI Setup Guide

## Important: You Need an Azure OpenAI Resource

The endpoint you're currently using (`https://dom-ag-scope.cognitiveservices.azure.com/`) is for **Azure Document Intelligence**, which is a different service.

For this application, you need an **Azure OpenAI** resource, which has a different endpoint format.

## How to Get Your Azure OpenAI Endpoint

### Step 1: Create or Find Your Azure OpenAI Resource

1. Go to the [Azure Portal](https://portal.azure.com)
2. Search for "Azure OpenAI" in the top search bar
3. Either:
   - **Create a new resource** (if you don't have one)
   - **Open your existing Azure OpenAI resource**

### Step 2: Get Your Endpoint and Key

1. In your Azure OpenAI resource, go to **"Keys and Endpoint"** (or **"Resource Management"** → **"Keys and Endpoint"**)
2. You'll see:
   - **Endpoint**: Should look like `https://your-resource-name.openai.azure.com/`
   - **Key 1** or **Key 2**: Copy one of these

### Step 3: Check Your Deployment

1. Go to **"Model deployments"** or **"Deployments"** in your Azure OpenAI resource
2. Make sure you have a deployment (e.g., "gpt-4", "gpt-35-turbo", etc.)
3. Note the exact deployment name (case-sensitive)

### Step 4: Update appsettings.json

Update your `appsettings.json` with:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource-name.openai.azure.com/",
    "ApiKey": "your-key-1-or-key-2-here",
    "DeploymentName": "gpt-4"
  }
}
```

## Endpoint Format

- ✅ **Correct**: `https://your-resource.openai.azure.com/`
- ❌ **Wrong**: `https://your-resource.cognitiveservices.azure.com/` (This is Document Intelligence)

## Common Issues

1. **401 Error**: Usually means wrong endpoint or API key
2. **404 Error**: Usually means wrong deployment name
3. **Endpoint format**: Must end with `.openai.azure.com/`

## Testing

After updating your configuration, test the connection:
```
GET http://localhost:5000/api/analysis/test-openai
```

## Note About Document Intelligence

If you want to use **Azure Document Intelligence** for better PDF text extraction (instead of iText7), that's a separate integration. However, you still need Azure OpenAI for the intelligent analysis of the extracted text.

