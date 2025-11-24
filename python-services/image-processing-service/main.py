"""
Image Processing Service for Drainage Plan Analysis
Provides computer vision operations using OpenCV
"""

from fastapi import FastAPI, File, UploadFile, HTTPException, Form
from fastapi.responses import JSONResponse
from fastapi.middleware.cors import CORSMiddleware
import cv2
import numpy as np
from typing import List, Optional
import base64
import io
from PIL import Image
import logging

# Configure logging first
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Try to import pdf2image, but make it optional
try:
    from pdf2image import convert_from_bytes
    PDF2IMAGE_AVAILABLE = True
except ImportError:
    PDF2IMAGE_AVAILABLE = False
    logger.warning("pdf2image not available. PDF to image conversion will not work. Install with: pip install pdf2image")

app = FastAPI(title="Image Processing Service", version="1.0.0")

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


def image_from_bytes(image_bytes: bytes) -> np.ndarray:
    """Convert image bytes to OpenCV format (numpy array)"""
    nparr = np.frombuffer(image_bytes, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    if img is None:
        raise ValueError("Could not decode image")
    return img


def image_to_base64(image: np.ndarray) -> str:
    """Convert OpenCV image to base64 string"""
    _, buffer = cv2.imencode('.png', image)
    img_base64 = base64.b64encode(buffer).decode('utf-8')
    return img_base64


@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {"status": "healthy", "service": "image-processing-service"}


@app.post("/detect-lines")
async def detect_lines(file: UploadFile = File(...)):
    """
    Detect lines in an image using Hough Line Transform
    Returns detected line segments with their endpoints
    """
    try:
        image_bytes = await file.read()
        img = image_from_bytes(image_bytes)
        
        # Convert to grayscale
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        
        # Apply edge detection
        edges = cv2.Canny(gray, 50, 150, apertureSize=3)
        
        # Detect lines using Hough Line Transform
        lines = cv2.HoughLinesP(edges, 1, np.pi/180, threshold=100, 
                                minLineLength=50, maxLineGap=10)
        
        line_segments = []
        if lines is not None:
            for line in lines:
                x1, y1, x2, y2 = line[0]
                line_segments.append({
                    "startPoint": {"x": int(x1), "y": int(y1)},
                    "endPoint": {"x": int(x2), "y": int(y2)}
                })
        
        return JSONResponse(content={
            "lines": line_segments,
            "count": len(line_segments)
        })
    
    except Exception as e:
        logger.error(f"Error detecting lines: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Error detecting lines: {str(e)}")


@app.post("/detect-circles")
async def detect_circles(file: UploadFile = File(...)):
    """
    Detect circles in an image using Hough Circle Transform
    Returns detected circles with center and radius
    """
    try:
        image_bytes = await file.read()
        img = image_from_bytes(image_bytes)
        
        # Convert to grayscale
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        
        # Apply Gaussian blur
        gray = cv2.medianBlur(gray, 5)
        
        # Detect circles
        circles = cv2.HoughCircles(gray, cv2.HOUGH_GRADIENT, 1, 20,
                                  param1=50, param2=30, minRadius=10, maxRadius=200)
        
        detected_circles = []
        if circles is not None:
            circles = np.uint16(np.around(circles))
            for circle in circles[0, :]:
                center_x, center_y, radius = circle
                detected_circles.append({
                    "center": {"x": int(center_x), "y": int(center_y)},
                    "radius": int(radius)
                })
        
        return JSONResponse(content={
            "circles": detected_circles,
            "count": len(detected_circles)
        })
    
    except Exception as e:
        logger.error(f"Error detecting circles: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Error detecting circles: {str(e)}")


@app.post("/detect-rectangles")
async def detect_rectangles(file: UploadFile = File(...)):
    """
    Detect rectangles in an image using contour detection
    Returns detected rectangles with bounding boxes
    """
    try:
        image_bytes = await file.read()
        img = image_from_bytes(image_bytes)
        
        # Convert to grayscale
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        
        # Apply threshold
        _, thresh = cv2.threshold(gray, 127, 255, cv2.THRESH_BINARY)
        
        # Find contours
        contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        
        rectangles = []
        for contour in contours:
            # Approximate contour to polygon
            epsilon = 0.02 * cv2.arcLength(contour, True)
            approx = cv2.approxPolyDP(contour, epsilon, True)
            
            # Check if it's a rectangle (4 vertices)
            if len(approx) == 4:
                x, y, w, h = cv2.boundingRect(approx)
                rectangles.append({
                    "x": int(x),
                    "y": int(y),
                    "width": int(w),
                    "height": int(h),
                    "points": [[int(p[0][0]), int(p[0][1])] for p in approx]
                })
        
        return JSONResponse(content={
            "rectangles": rectangles,
            "count": len(rectangles)
        })
    
    except Exception as e:
        logger.error(f"Error detecting rectangles: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Error detecting rectangles: {str(e)}")


@app.post("/crop-image")
async def crop_image(
    file: UploadFile = File(...),
    x: int = 0,
    y: int = 0,
    width: int = 0,
    height: int = 0
):
    """
    Crop an image to the specified bounding box
    Returns cropped image as base64 string
    """
    try:
        image_bytes = await file.read()
        img = image_from_bytes(image_bytes)
        
        # Validate crop parameters
        img_height, img_width = img.shape[:2]
        if x < 0 or y < 0 or x + width > img_width or y + height > img_height:
            raise HTTPException(status_code=400, detail="Crop parameters out of bounds")
        
        # Crop image
        cropped = img[y:y+height, x:x+width]
        
        # Convert to base64
        img_base64 = image_to_base64(cropped)
        
        return JSONResponse(content={
            "croppedImage": img_base64,
            "width": width,
            "height": height
        })
    
    except Exception as e:
        logger.error(f"Error cropping image: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Error cropping image: {str(e)}")


def detect_double_rectangles(img: np.ndarray) -> List[dict]:
    """
    Detect double-framed rectangles (nested rectangles) in an image.
    This is used to identify module symbols on drainage plans.
    
    Algorithm:
    1. Convert to grayscale and apply edge detection
    2. Find all contours
    3. For each contour, check if it's a rectangle
    4. Check if there's a nested rectangle inside (double frame)
    5. Return bounding boxes with confidence scores
    """
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    
    # Apply adaptive thresholding for better edge detection
    thresh = cv2.adaptiveThreshold(
        gray, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, 
        cv2.THRESH_BINARY_INV, 11, 2
    )
    
    # Find contours
    contours, hierarchy = cv2.findContours(
        thresh, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE
    )
    
    detected_symbols = []
    
    if hierarchy is None:
        return detected_symbols
    
    # Process each contour
    for i, contour in enumerate(contours):
        # Skip small contours
        area = cv2.contourArea(contour)
        if area < 500:  # Minimum area threshold
            continue
        
        # Approximate contour to polygon
        epsilon = 0.02 * cv2.arcLength(contour, True)
        approx = cv2.approxPolyDP(contour, epsilon, True)
        
        # Check if it's a rectangle (4 vertices)
        if len(approx) == 4:
            # Get bounding box
            x, y, w, h = cv2.boundingRect(approx)
            
            # Check for nested rectangle (child contour)
            has_nested = False
            confidence = 0.5  # Base confidence
            
            # Check hierarchy for child contours
            child_idx = hierarchy[0][i][2]  # First child
            if child_idx != -1:
                # Check if child is also a rectangle
                child_contour = contours[child_idx]
                child_area = cv2.contourArea(child_contour)
                
                # Child should be significantly smaller (nested)
                if child_area > 0 and child_area < area * 0.8:
                    child_epsilon = 0.02 * cv2.arcLength(child_contour, True)
                    child_approx = cv2.approxPolyDP(child_contour, child_epsilon, True)
                    
                    if len(child_approx) == 4:
                        # Check if child is inside parent
                        child_x, child_y, child_w, child_h = cv2.boundingRect(child_approx)
                        if (child_x > x and child_y > y and 
                            child_x + child_w < x + w and 
                            child_y + child_h < y + h):
                            has_nested = True
                            confidence = 0.85  # High confidence for double rectangle
            
            # Only include if it has nested rectangle (double frame)
            if has_nested:
                # Calculate bounding box points (4 corners)
                points = []
                for point in approx:
                    points.append([int(point[0][0]), int(point[0][1])])
                
                detected_symbols.append({
                    "type": "DoubleRectangle",
                    "boundingBox": {
                        "x": int(x),
                        "y": int(y),
                        "width": int(w),
                        "height": int(h),
                        "points": points
                    },
                    "confidence": confidence,
                    "area": int(area)
                })
    
    return detected_symbols


@app.post("/detect-symbols")
async def detect_symbols(file: UploadFile = File(...)):
    """
    Detect symbols in an image (double rectangles, circles with grids, ovals, etc.)
    Returns detected symbols with bounding boxes, types, and confidence scores
    """
    try:
        image_bytes = await file.read()
        img = image_from_bytes(image_bytes)
        
        detected_symbols = []
        
        # Detect double-framed rectangles
        double_rects = detect_double_rectangles(img)
        detected_symbols.extend(double_rects)
        
        # TODO: Add circle with grid detection (STEP-2.2)
        # TODO: Add oval detection (STEP-2.3)
        
        logger.info(f"Detected {len(detected_symbols)} symbols")
        
        return JSONResponse(content={
            "symbols": detected_symbols,
            "count": len(detected_symbols)
        })
    
    except Exception as e:
        logger.error(f"Error detecting symbols: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Error detecting symbols: {str(e)}")


@app.post("/convert-pdf-page")
async def convert_pdf_page(
    file: UploadFile = File(...),
    page_number: int = Form(1),
    dpi: int = Form(300)
):
    """
    Convert a specific page from a PDF to an image
    Returns the page image as base64-encoded PNG
    """
    if not PDF2IMAGE_AVAILABLE:
        raise HTTPException(
            status_code=501,
            detail="PDF to image conversion not available. Please install pdf2image: pip install pdf2image"
        )
    
    try:
        # Validate file type
        if not file.content_type or file.content_type != "application/pdf":
            # Check file extension as fallback
            if not file.filename or not file.filename.lower().endswith('.pdf'):
                raise HTTPException(status_code=400, detail="File must be a PDF")
        
        pdf_bytes = await file.read()
        
        if len(pdf_bytes) == 0:
            raise HTTPException(status_code=400, detail="PDF file is empty")
        
        logger.info(f"Converting PDF page {page_number} to image (DPI: {dpi})")
        
        # Convert PDF page to image
        images = convert_from_bytes(pdf_bytes, dpi=dpi, first_page=page_number, last_page=page_number)
        
        if not images or len(images) == 0:
            raise HTTPException(status_code=404, detail=f"Page {page_number} not found in PDF")
        
        # Convert PIL Image to OpenCV format
        img = images[0]
        img_cv = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)
        
        # Convert to base64
        img_base64 = image_to_base64(img_cv)
        
        logger.info(f"Successfully converted PDF page {page_number} to image")
        
        return JSONResponse(content={
            "image": img_base64,
            "pageNumber": page_number,
            "width": img.width,
            "height": img.height,
            "dpi": dpi
        })
    
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error converting PDF page to image: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Error converting PDF page to image: {str(e)}")


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8001)

