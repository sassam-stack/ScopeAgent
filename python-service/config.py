import os
from typing import Literal

class YoloConfig:
    """Configuration for YOLO models"""
    
    # Model selection: nano, small, medium, large, extra-large
    MODEL_SIZE: str = os.getenv("YOLO_MODEL_SIZE", "yolo11n")  # yolo11n, yolo11s, yolo11m, yolo11l, yolo11x
    
    # Confidence threshold (0.0 to 1.0)
    CONFIDENCE_THRESHOLD: float = float(os.getenv("YOLO_CONFIDENCE", "0.25"))
    
    # Device selection: 'cpu', 'cuda', 'mps' (for Mac)
    DEVICE: str = os.getenv("YOLO_DEVICE", "cpu")
    
    # IOU threshold for NMS
    IOU_THRESHOLD: float = float(os.getenv("YOLO_IOU", "0.45"))
    
    # Maximum image size (pixels)
    MAX_IMAGE_SIZE: int = int(os.getenv("YOLO_MAX_SIZE", "640"))
    
    @classmethod
    def get_model_name(cls) -> str:
        """Get the full model name"""
        return cls.MODEL_SIZE

