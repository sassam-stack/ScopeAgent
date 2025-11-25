# Image Processing Service

FastAPI microservice for computer vision operations using OpenCV.

## Setup

1. **Install system dependencies (required for PDF to image conversion):**
   
   **Windows:**
   - Download Poppler for Windows from: https://github.com/oschwartz10612/poppler-windows/releases/
   - Extract the ZIP file (e.g., to `C:\poppler`)
   - Add the `bin` folder to your system PATH:
     - Open System Properties → Environment Variables
     - Edit the `Path` variable in System variables
     - Add the path to the `bin` folder (e.g., `C:\poppler\Library\bin`)
     - Restart your terminal/Python service
   - Or install via chocolatey: `choco install poppler`
   - Or - Quick Test Without PATH (Temporary)
      - $env:PATH += ";C:\path\to\poppler\Library\bin"
   - **Verify installation:** Open a new terminal and run `pdftoppm -h` - it should show help text
   
   **Linux (Ubuntu/Debian):**
   ```bash
   sudo apt-get update
   sudo apt-get install poppler-utils
   ```
   
   **macOS:**
   ```bash
   brew install poppler
   ```

2. **Install Python dependencies:**
   ```bash
   pip install -r requirements.txt
   ```

3. **Start the service:**
   ```bash
   python main.py
   ```
   
   Or using uvicorn directly:
   ```bash
   uvicorn main:app --host 0.0.0.0 --port 8001
   ```

The service will be available at `http://localhost:8001`

**Note:** If `pdf2image` is not installed or Poppler is not available, PDF to image conversion will return 501 Not Implemented. The service will still work for other image processing operations.

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

### `POST /convert-pdf-page`
Convert a PDF page to an image.

**Request:**
- Multipart form data with `file` field containing PDF file
- Query parameters: `page_number` (default: 1), `dpi` (default: 300)

**Response:**
```json
{
  "image": "base64_encoded_image_string",
  "pageNumber": 1,
  "width": 2550,
  "height": 3300,
  "dpi": 300
}
```

**Note:** Requires `pdf2image` Python package and Poppler system library to be installed.

### `POST /detect-symbols`
Detect symbols in an image (double rectangles, circles with grids, ovals, etc.).

**Request:** Multipart form data with `file` field containing image

**Response:**
```json
{
  "symbols": [
    {
      "type": "DoubleRectangle",
      "boundingBox": {
        "x": 100,
        "y": 100,
        "width": 200,
        "height": 150,
        "points": [[100, 100], [300, 100], [300, 250], [100, 250]]
      },
      "confidence": 0.85,
      "area": 30000
    }
  ],
  "count": 1
}
```

## Configuration

Set environment variables to configure the service:

- `PORT`: Port to run the service on (default: 8001)

## Notes

- Images are processed in memory
- Supports common image formats (JPEG, PNG, etc.)
- OpenCV operations are CPU-intensive; consider scaling for production

