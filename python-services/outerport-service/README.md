# Outerport Service

Python microservice for processing drainage plans using the Outerport API.

## Setup

1. Install dependencies:
```bash
pip install -r requirements.txt
```

2. Set environment variables:
```bash
export OUTERPORT_API_KEY=your_api_key_here
export OUTERPORT_BASE_URL=http://localhost:8080  # Optional, defaults to localhost:8080
```

3. Run the service:
```bash
python main.py
```

Or using uvicorn:
```bash
uvicorn main:app --host 0.0.0.0 --port 8002
```

## API Endpoints

### POST `/process-drainage-plan`

Process a drainage plan image through the Outerport API.

**Request:**
- Multipart form data with `file` containing the image
- Optional header: `X-API-Key` (if not provided, uses `OUTERPORT_API_KEY` env var)
- Optional query params:
  - `base_url`: Override base URL (defaults to env var or localhost:8080)
  - `poll_interval`: Seconds between status polls (default: 2.0)
  - `timeout`: Maximum seconds to wait (default: 300.0)

**Response:**
```json
{
  "junctions": [
    {
      "id": "S-1",
      "bbox": [x1, y1, x2, y2] or null,
      "label_bbox": [x1, y1, x2, y2],
      "expected_directions": ["N", "E", "NIE"] or null
    }
  ],
  "materials": [
    {
      "text": "VU100",
      "bbox": [x1, y1, x2, y2]
    }
  ]
}
```

## Configuration

The service can be configured via environment variables:
- `OUTERPORT_API_KEY`: Required. Your Outerport API key
- `OUTERPORT_BASE_URL`: Optional. Base URL of the Outerport API (default: http://localhost:8080)

## Health Check

### GET `/health`

Returns service health status.
