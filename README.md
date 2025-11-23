# Scope Agent - Construction Drainage Plan Analyzer

A full-stack application for analyzing construction drainage plans from PDF documents stored in Azure Blob Storage. The application uses AI to identify drainage schemas, detect compass directions, locate modules, and extract metadata.

## Architecture

- **Backend**: ASP.NET Core 8.0 Web API (C#)
- **Frontend**: React 18 with Vite
- **AI Services**: 
  - Azure Computer Vision API (image analysis, OCR)
  - YOLO Python Microservice (object detection, segmentation, pose estimation, classification)
- **Storage**: Azure Blob Storage

## Features

1. **PDF Analysis**: Downloads and processes PDFs from Azure Blob Storage URLs
2. **Drainage Schema Detection**: Identifies pages containing drainage system schemas
3. **Compass Detection**: Detects north direction indicators on schema pages
4. **Module Extraction**: Locates and extracts metadata for modules (m1, m2, etc.)
5. **Table Location**: Identifies relevant tables and their positions in the document
6. **Structured Results**: Returns comprehensive analysis in JSON format

## Prerequisites

- .NET 8.0 SDK
- Node.js 18+ and npm
- Python 3.8+ (for YOLO microservice)
- Azure Computer Vision account with API key and endpoint
- Azure Blob Storage account (optional, for storing images)

## Setup Instructions

### Python YOLO Service Setup

1. Navigate to the Python service directory:
   ```bash
   cd python-service
   ```

2. Install Python dependencies:
   ```bash
   pip install -r requirements.txt
   ```

3. Start the YOLO service:
   ```bash
   python main.py
   ```
   
   Or using uvicorn:
   ```bash
   uvicorn main:app --host 0.0.0.0 --port 8000
   ```

   The service will be available at `http://localhost:8000`
   
   Note: Models will be downloaded automatically on first run.

### Backend Setup

1. Navigate to the API directory:
   ```bash
   cd src/ScopeAgent.Api
   ```

2. Configure services in `appsettings.json`:
   ```json
   {
     "ComputerVision": {
       "Endpoint": "https://your-resource.cognitiveservices.azure.com/",
       "ApiKey": "your-computer-vision-api-key"
     },
     "Yolo": {
       "ServiceUrl": "http://localhost:8000",
       "TimeoutSeconds": 60
     }
   }
   ```

3. Restore packages:
   ```bash
   dotnet restore
   ```

4. Run the API:
   ```bash
   dotnet run
   ```

   The API will be available at `https://localhost:5000` (or `http://localhost:5000`)

### Frontend Setup

1. Navigate to the frontend directory:
   ```bash
   cd frontend
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Create a `.env` file (optional, defaults to `http://localhost:5000/api`):
   ```
   VITE_API_URL=http://localhost:5000/api
   ```

4. Run the development server:
   ```bash
   npm run dev
   ```

   The frontend will be available at `http://localhost:3000`

## Usage

1. **Start the Python YOLO service** (if using YOLO features):
   ```bash
   cd python-service
   python main.py
   ```

2. **Start the C# backend API**:
   ```bash
   cd src/ScopeAgent.Api
   dotnet run
   ```

3. **Start the frontend**:
   ```bash
   cd frontend
   npm run dev
   ```

4. Open the application in your browser at `http://localhost:5173`
5. Click "Select Image" and choose an image file
6. Wait for the analysis to complete
7. Review the comprehensive results showing:
   - **Azure Computer Vision**: Captions, tags, categories, colors, OCR text
   - **YOLO Analysis**: Object detection, segmentation, pose estimation, classification

## API Endpoints

### POST `/api/analysis/analyze`

Analyzes an uploaded image file using Azure Computer Vision and YOLO.

**Request:** Multipart form data with `file` field containing image

**Response:** Combined results from Computer Vision and YOLO:
```json
{
  "caption": { "text": "...", "confidence": 0.99 },
  "tags": [...],
  "categories": [...],
  "color": {...},
  "text": { "content": "...", "pages": [...] },
  "yolo": {
    "detection": { "objects": [...], "count": N },
    "segmentation": { "masks": [...], "count": N },
    "pose": { "keypoints": [...], "count": N },
    "classification": { "classes": [...], "top": {...} }
  }
}
```

### GET `/api/analysis/health`

Health check endpoint.

## Project Structure

```
ScopeAgent/
├── python-service/
│   ├── main.py
│   ├── yolo_service.py
│   ├── config.py
│   ├── requirements.txt
│   └── README.md
├── src/
│   └── ScopeAgent.Api/
│       ├── Controllers/
│       ├── Models/
│       ├── Services/
│       └── Program.cs
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   ├── App.jsx
│   │   └── main.jsx
│   └── package.json
└── README.md
```

## Development

### Backend Development

- The API uses Swagger UI in development mode
- Access Swagger at `https://localhost:5000/swagger`
- CORS is configured to allow requests from `http://localhost:3000` and `http://localhost:5173`

### Frontend Development

- Uses Vite for fast development
- Hot module replacement enabled
- Proxy configured to forward `/api` requests to the backend

## Troubleshooting

1. **CORS Errors**: Ensure the frontend URL is in the CORS policy in `Program.cs`
2. **Computer Vision Errors**: Verify your endpoint and API key in `appsettings.json`
3. **YOLO Service Errors**: 
   - Ensure Python service is running on port 8000
   - Check that Python dependencies are installed
   - Verify YOLO service URL in `appsettings.json` matches the running service
   - First request may be slow as models are downloaded and loaded
4. **Port Conflicts**: Change ports in `vite.config.js` (frontend), `launchSettings.json` (backend), or `main.py` (Python service)
5. **Image Size Errors**: Images are automatically resized if they exceed Azure Computer Vision limits (4MB, 10000x10000px)

## License

This project is for development and testing purposes.

