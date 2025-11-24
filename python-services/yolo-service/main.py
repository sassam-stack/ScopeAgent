from fastapi import FastAPI, File, UploadFile, HTTPException, Form
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from typing import Optional
import uvicorn
from yolo_service import YoloService
import logging

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="YOLO Image Analysis Service", version="1.0.0")

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # In production, specify actual origins
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Initialize YOLO service
yolo_service = YoloService()

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {"status": "healthy", "service": "YOLO Analysis"}

@app.post("/analyze")
async def analyze_all(
    file: UploadFile = File(...),
    context: Optional[str] = Form(None)
):
    """
    Run all YOLO analyses (detection, segmentation, pose, classification) on uploaded image
    Optional context parameter to provide image description/context
    """
    try:
        if not file.content_type or not file.content_type.startswith('image/'):
            raise HTTPException(status_code=400, detail="File must be an image")
        
        image_bytes = await file.read()
        
        if len(image_bytes) == 0:
            raise HTTPException(status_code=400, detail="Image file is empty")
        
        logger.info(f"Processing image: {file.filename}, size: {len(image_bytes)} bytes")
        if context:
            logger.info(f"Context provided: {context[:100]}...")
        
        result = yolo_service.analyze_all(image_bytes, context)
        
        return JSONResponse(content=result)
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error processing image: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Error processing image: {str(e)}")

@app.post("/analyze/detect")
async def analyze_detect(file: UploadFile = File(...)):
    """
    Run object detection only
    """
    try:
        if not file.content_type or not file.content_type.startswith('image/'):
            raise HTTPException(status_code=400, detail="File must be an image")
        
        image_bytes = await file.read()
        
        if len(image_bytes) == 0:
            raise HTTPException(status_code=400, detail="Image file is empty")
        
        result = yolo_service.detect(image_bytes)
        
        return JSONResponse(content=result)
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error in detection: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Error in detection: {str(e)}")

@app.post("/analyze/segment")
async def analyze_segment(file: UploadFile = File(...)):
    """
    Run instance segmentation only
    """
    try:
        if not file.content_type or not file.content_type.startswith('image/'):
            raise HTTPException(status_code=400, detail="File must be an image")
        
        image_bytes = await file.read()
        
        if len(image_bytes) == 0:
            raise HTTPException(status_code=400, detail="Image file is empty")
        
        result = yolo_service.segment(image_bytes)
        
        return JSONResponse(content=result)
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error in segmentation: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Error in segmentation: {str(e)}")

@app.post("/analyze/pose")
async def analyze_pose(file: UploadFile = File(...)):
    """
    Run pose estimation only
    """
    try:
        if not file.content_type or not file.content_type.startswith('image/'):
            raise HTTPException(status_code=400, detail="File must be an image")
        
        image_bytes = await file.read()
        
        if len(image_bytes) == 0:
            raise HTTPException(status_code=400, detail="Image file is empty")
        
        result = yolo_service.pose(image_bytes)
        
        return JSONResponse(content=result)
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error in pose estimation: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Error in pose estimation: {str(e)}")

@app.post("/analyze/classify")
async def analyze_classify(file: UploadFile = File(...)):
    """
    Run image classification only
    """
    try:
        if not file.content_type or not file.content_type.startswith('image/'):
            raise HTTPException(status_code=400, detail="File must be an image")
        
        image_bytes = await file.read()
        
        if len(image_bytes) == 0:
            raise HTTPException(status_code=400, detail="Image file is empty")
        
        result = yolo_service.classify(image_bytes)
        
        return JSONResponse(content=result)
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error in classification: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Error in classification: {str(e)}")

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)

