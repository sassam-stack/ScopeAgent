# Python Services

This directory contains all Python microservices used by the ScopeAgent application.

## Services

### yolo-service
YOLO image analysis service for object detection, segmentation, pose estimation, and classification.

**Status:** Currently disabled in backend (preserved for future use)

**Location:** `yolo-service/`

## Adding New Services

When adding a new Python service:

1. Create a new subdirectory: `python-services/your-service-name/`
2. Include:
   - `main.py` - FastAPI/Flask application
   - `requirements.txt` - Python dependencies
   - `README.md` - Service documentation
   - Service-specific code
3. Update this README with service information
4. Integrate with backend API as needed

## Running Services

Each service should be run independently:

```bash
cd python-services/your-service-name
pip install -r requirements.txt
python main.py
```

Or using uvicorn (for FastAPI):

```bash
uvicorn main:app --host 0.0.0.0 --port 8000
```

