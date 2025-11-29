"""
Outerport API Service for Drainage Plan Analysis
Provides integration with Outerport API for drainage plan module extraction
"""

from fastapi import FastAPI, File, UploadFile, HTTPException, Query, Header
from fastapi.responses import JSONResponse
from fastapi.middleware.cors import CORSMiddleware
import json
import os
import time
import requests
from typing import Optional
import logging

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="Outerport Service", version="1.0.0")

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


def get_api_key() -> str:
    """Get API key from environment variable"""
    api_key = os.environ.get("OUTERPORT_API_KEY")
    if not api_key:
        raise ValueError("OUTERPORT_API_KEY environment variable not set")
    return api_key


def get_base_url() -> str:
    """Get base URL from environment variable or use default"""
    return os.environ.get("OUTERPORT_BASE_URL", "http://localhost:8080")


@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {"status": "healthy", "service": "outerport-service"}


@app.post("/process-drainage-plan")
async def process_drainage_plan(
    file: UploadFile = File(...),
    api_key: Optional[str] = Header(None, alias="X-API-Key"),
    base_url: Optional[str] = Query(None),
    poll_interval: float = Query(2.0, ge=0.5, le=10.0),
    timeout: float = Query(300.0, ge=10.0, le=600.0)
):
    """
    Process a drainage plan image through the Outerport API.
    
    Returns:
        Dictionary containing drainage plan annotations with structure:
        {
            "junctions": [
                {
                    "id": "S-1",
                    "bbox": [x1, y1, x2, y2] or null,
                    "label_bbox": [x1, y1, x2, y2],
                    "expected_directions": ["N", "E", "NIE"] or null
                },
                ...
            ],
            "materials": [
                {
                    "text": "VU100",
                    "bbox": [x1, y1, x2, y2]
                },
                ...
            ]
        }
    """
    try:
        # Get API key from header or environment variable
        if not api_key:
            api_key = get_api_key()
        
        # Get base URL from query param or environment variable
        if not base_url:
            base_url = get_base_url()
        
        # Ensure base_url doesn't end with /
        base_url = base_url.rstrip('/')
        
        logger.info(f"Processing drainage plan image: {file.filename}")
        logger.info(f"Using base URL: {base_url}")
        
        headers = {
            "Authorization": f"Bearer {api_key}",
        }
        
        # Read image file
        image_bytes = await file.read()
        
        # Step 1: Upload image and start processing
        logger.info("Uploading image to Outerport API...")
        files = {"image": (file.filename or "image.png", image_bytes, file.content_type or "image/png")}
        params = {"component_type": "drainage_plan"}
        
        response = requests.post(
            f"{base_url}/api/v0/components",
            headers=headers,
            params=params,
            files=files,
            timeout=30
        )
        
        if response.status_code != 200:
            error_msg = response.text
            logger.error(f"Failed to upload image: {response.status_code} - {error_msg}")
            raise HTTPException(
                status_code=response.status_code,
                detail=f"Failed to upload image to Outerport API: {error_msg}"
            )
        
        result = response.json()
        job_status_id = result["job_status_id"]
        component_id = result["component_id"]
        
        logger.info(f"Job started: job_status_id={job_status_id}, component_id={component_id}")
        
        # Step 2: Poll for job completion
        logger.info("Polling for job completion...")
        start_time = time.time()
        
        while True:
            elapsed = time.time() - start_time
            if elapsed > timeout:
                raise HTTPException(
                    status_code=408,
                    detail=f"Processing timed out after {timeout} seconds"
                )
            
            response = requests.get(
                f"{base_url}/api/v0/job-statuses/{job_status_id}",
                headers=headers,
                timeout=10
            )
            
            if response.status_code != 200:
                error_msg = response.text
                logger.error(f"Failed to get job status: {response.status_code} - {error_msg}")
                raise HTTPException(
                    status_code=response.status_code,
                    detail=f"Failed to get job status: {error_msg}"
                )
            
            status_result = response.json()
            status = status_result["status"]
            
            if status == "done":
                logger.info(f"Processing completed in {elapsed:.1f} seconds")
                break
            elif status == "error":
                error_msg = status_result.get("error_message", "Unknown error")
                logger.error(f"Processing failed: {error_msg}")
                raise HTTPException(
                    status_code=500,
                    detail=f"Outerport processing failed: {error_msg}"
                )
            else:
                logger.debug(f"Status: {status} ({elapsed:.1f}s elapsed)")
                time.sleep(poll_interval)
        
        # Step 3: Get the processed component
        logger.info("Retrieving drainage plan annotations...")
        response = requests.get(
            f"{base_url}/api/v0/components/{component_id}",
            headers=headers,
            timeout=30
        )
        
        if response.status_code != 200:
            error_msg = response.text
            logger.error(f"Failed to get component: {response.status_code} - {error_msg}")
            raise HTTPException(
                status_code=response.status_code,
                detail=f"Failed to get component: {error_msg}"
            )
        
        component = response.json()
        
        # The annotations are stored in the 'content' field as JSON
        content_str = component.get("content")
        if isinstance(content_str, str):
            annotations = json.loads(content_str)
        else:
            annotations = content_str
        
        logger.info(f"Successfully retrieved annotations: {len(annotations.get('junctions', []))} junctions found")
        
        return JSONResponse(content=annotations)
    
    except HTTPException:
        raise
    except ValueError as e:
        logger.error(f"Configuration error: {str(e)}")
        raise HTTPException(status_code=500, detail=str(e))
    except requests.RequestException as e:
        logger.error(f"Request error: {str(e)}")
        raise HTTPException(status_code=503, detail=f"Error communicating with Outerport API: {str(e)}")
    except Exception as e:
        logger.error(f"Unexpected error processing drainage plan: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Unexpected error: {str(e)}")


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8002)
