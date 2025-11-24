# YOLO Python Microservice

FastAPI microservice for running Ultralytics YOLO models on images.

**⚠️ NOTE: This service is currently disabled in the backend. It is preserved for future use but not actively called by the API.**

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
   uvicorn main:app --host 0.0.0.0 --port 8000
   ```

The service will automatically download YOLO models on first run.

## Configuration

Set environment variables to configure the service:

- `YOLO_MODEL_SIZE`: Model size (default: `yolo11n`)
  - Options: `yolo11n`, `yolo11s`, `yolo11m`, `yolo11l`, `yolo11x`
- `YOLO_CONFIDENCE`: Confidence threshold (default: `0.25`)
- `YOLO_DEVICE`: Device to use (default: `cpu`)
  - Options: `cpu`, `cuda`, `mps`
- `YOLO_IOU`: IOU threshold for NMS (default: `0.45`)
- `YOLO_MAX_SIZE`: Maximum image size (default: `640`)

## API Endpoints

- `GET /health` - Health check
- `POST /analyze` - Run all analyses (detection, segmentation, pose, classification)
- `POST /analyze/detect` - Object detection only
- `POST /analyze/segment` - Instance segmentation only
- `POST /analyze/pose` - Pose estimation only
- `POST /analyze/classify` - Image classification only

## Example Usage

```bash
curl -X POST "http://localhost:8000/analyze" \
  -F "file=@image.jpg"
```

## Notes

- Models are downloaded automatically on first use
- First request may be slower as models are loaded into memory
- GPU support requires CUDA-compatible PyTorch installation
- **This service is currently disabled in the backend API**
