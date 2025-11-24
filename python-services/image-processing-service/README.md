# Image Processing Service

FastAPI microservice for computer vision operations using OpenCV.

## Setup

1. **Install Python dependencies:**
   ```bash
   pip install -r requirements.txt
   ```

2. **Start the service:**
   ```bash
   python main.py
   ```
   
   Or using uvicorn directly:
   ```bash
   uvicorn main:app --host 0.0.0.0 --port 8001
   ```

The service will be available at `http://localhost:8001`

## API Endpoints

### `GET /health`
Health check endpoint.

### `POST /detect-lines`
Detect lines in an image using Hough Line Transform.

**Request:** Multipart form data with `file` field containing image

**Response:**
```json
{
  "lines": [
    {
      "startPoint": {"x": 100, "y": 200},
      "endPoint": {"x": 300, "y": 400}
    }
  ],
  "count": 1
}
```

### `POST /detect-circles`
Detect circles in an image using Hough Circle Transform.

**Request:** Multipart form data with `file` field containing image

**Response:**
```json
{
  "circles": [
    {
      "center": {"x": 150, "y": 150},
      "radius": 50
    }
  ],
  "count": 1
}
```

### `POST /detect-rectangles`
Detect rectangles in an image using contour detection.

**Request:** Multipart form data with `file` field containing image

**Response:**
```json
{
  "rectangles": [
    {
      "x": 100,
      "y": 100,
      "width": 200,
      "height": 150,
      "points": [[100, 100], [300, 100], [300, 250], [100, 250]]
    }
  ],
  "count": 1
}
```

### `POST /crop-image`
Crop an image to the specified bounding box.

**Request:** 
- Multipart form data with `file` field containing image
- Query parameters: `x`, `y`, `width`, `height`

**Response:**
```json
{
  "croppedImage": "base64_encoded_image_string",
  "width": 200,
  "height": 150
}
```

## Configuration

Set environment variables to configure the service:

- `PORT`: Port to run the service on (default: 8001)

## Notes

- Images are processed in memory
- Supports common image formats (JPEG, PNG, etc.)
- OpenCV operations are CPU-intensive; consider scaling for production

