import io
import json
from typing import Dict, List, Any, Optional
from PIL import Image
import numpy as np
from ultralytics import YOLO
try:
    from config import YoloConfig
except ImportError:
    # Fallback for different import styles
    import sys
    import os
    sys.path.insert(0, os.path.dirname(__file__))
    from config import YoloConfig

class YoloService:
    """Service class for YOLO model inference"""
    
    def __init__(self):
        self.config = YoloConfig()
        self.models = {}
        self._load_models()
    
    def _load_models(self):
        """Load YOLO models for different tasks"""
        try:
            model_name = self.config.get_model_name()
            
            # Load detection model (also supports segmentation)
            self.models['detect'] = YOLO(f"{model_name}.pt")
            self.models['segment'] = YOLO(f"{model_name}-seg.pt")
            self.models['pose'] = YOLO(f"{model_name}-pose.pt")
            self.models['classify'] = YOLO(f"{model_name}-cls.pt")
            
            print(f"Loaded YOLO models: {model_name}")
        except Exception as e:
            print(f"Error loading YOLO models: {e}")
            # Fallback to detection model only
            model_name = self.config.get_model_name()
            self.models['detect'] = YOLO(f"{model_name}.pt")
    
    def _image_from_bytes(self, image_bytes: bytes) -> Image.Image:
        """Convert bytes to PIL Image"""
        return Image.open(io.BytesIO(image_bytes))
    
    def detect(self, image_bytes: bytes) -> Dict[str, Any]:
        """Run object detection on image"""
        try:
            image = self._image_from_bytes(image_bytes)
            model = self.models.get('detect')
            
            if model is None:
                return {"error": "Detection model not loaded"}
            
            # Get image dimensions for context
            img_width, img_height = image.size
            
            # Use very low confidence threshold to extract maximum information
            # Try multiple thresholds to get all possible detections
            results = model.predict(
                image,
                conf=0.01,  # Extremely low threshold to get everything
                iou=self.config.IOU_THRESHOLD,
                device=self.config.DEVICE,
                verbose=False
            )
            
            detections = []
            all_detections_low_conf = []  # Track all detections for reference
            
            for result in results:
                boxes = result.boxes
                
                # Get ALL detections regardless of confidence
                for i in range(len(boxes)):
                    box = boxes[i]
                    cls = int(box.cls[0])
                    conf = float(box.conf[0])
                    
                    # Include ALL detections - no filtering
                    xyxy = box.xyxy[0].cpu().numpy().tolist()
                    
                    detection = {
                        "class": result.names[cls],
                        "class_id": cls,
                        "confidence": conf,
                        "bounding_box": {
                            "x1": float(xyxy[0]),
                            "y1": float(xyxy[1]),
                            "x2": float(xyxy[2]),
                            "y2": float(xyxy[3]),
                            "width": float(xyxy[2] - xyxy[0]),
                            "height": float(xyxy[3] - xyxy[1])
                        }
                    }
                    
                    # Add ALL detections to the list
                    detections.append(detection)
                    
                    # Track all for reference
                    all_detections_low_conf.append({
                        "class": result.names[cls],
                        "confidence": conf
                    })
            
            # Sort all detections by confidence for debug info
            all_detections_sorted = sorted(all_detections_low_conf, key=lambda x: x['confidence'], reverse=True)
            
            # Sort detections by confidence (highest first)
            detections_sorted = sorted(detections, key=lambda x: x['confidence'], reverse=True)
            
            result_dict = {
                "objects": detections_sorted,
                "count": len(detections_sorted),
                "image_info": {
                    "width": img_width,
                    "height": img_height
                },
                "config": {
                    "confidence_threshold": 0.01,  # Actual threshold used
                    "iou_threshold": self.config.IOU_THRESHOLD,
                    "note": "All detections included - no filtering applied"
                }
            }
            
            return result_dict
        except Exception as e:
            import traceback
            return {"error": f"{str(e)}\n{traceback.format_exc()}"}
    
    def segment(self, image_bytes: bytes) -> Dict[str, Any]:
        """Run instance segmentation on image"""
        try:
            image = self._image_from_bytes(image_bytes)
            model = self.models.get('segment')
            
            if model is None:
                # Try using detect model if segment model not available
                model = self.models.get('detect')
                if model is None:
                    return {"error": "Segmentation model not loaded"}
            
            # Use very low confidence to get all segments
            results = model.predict(
                image,
                conf=0.01,  # Extremely low threshold to get everything
                iou=self.config.IOU_THRESHOLD,
                device=self.config.DEVICE,
                verbose=False
            )
            
            segments = []
            for result in results:
                if result.masks is not None:
                    boxes = result.boxes
                    masks = result.masks
                    
                    for i in range(len(boxes)):
                        box = boxes[i]
                        cls = int(box.cls[0])
                        conf = float(box.conf[0])
                        xyxy = box.xyxy[0].cpu().numpy().tolist()
                        
                        # Get mask data - handle both tensor and numpy array cases
                        mask = masks[i]
                        try:
                            # Try as tensor first
                            if hasattr(mask.data[0], 'cpu'):
                                mask_data = mask.data[0].cpu().numpy()
                            else:
                                # Already a numpy array
                                mask_data = np.array(mask.data[0])
                        except:
                            # Fallback: try direct access
                            mask_data = np.array(mask.data[0]) if hasattr(mask.data[0], '__array__') else mask.data[0]
                        
                        try:
                            # Try as tensor first
                            if hasattr(mask.xy[0], 'cpu'):
                                mask_points = mask.xy[0].cpu().numpy().tolist()
                            else:
                                # Already a numpy array
                                mask_points = np.array(mask.xy[0]).tolist()
                        except:
                            # Fallback: try direct access
                            mask_points = np.array(mask.xy[0]).tolist() if hasattr(mask.xy[0], '__array__') else mask.xy[0].tolist()
                        
                        segments.append({
                            "class": result.names[cls],
                            "class_id": cls,
                            "confidence": conf,
                            "bounding_box": {
                                "x1": float(xyxy[0]),
                                "y1": float(xyxy[1]),
                                "x2": float(xyxy[2]),
                                "y2": float(xyxy[3]),
                                "width": float(xyxy[2] - xyxy[0]),
                                "height": float(xyxy[3] - xyxy[1])
                            },
                            "mask_points": mask_points,
                            "mask_area": float(np.sum(mask_data))
                        })
            
            return {
                "masks": segments,
                "count": len(segments)
            }
        except Exception as e:
            import traceback
            return {"error": f"{str(e)}\n{traceback.format_exc()}"}
    
    def pose(self, image_bytes: bytes) -> Dict[str, Any]:
        """Run pose estimation on image"""
        try:
            image = self._image_from_bytes(image_bytes)
            model = self.models.get('pose')
            
            if model is None:
                return {"error": "Pose estimation model not loaded"}
            
            # Use very low confidence to get all poses
            results = model.predict(
                image,
                conf=0.01,  # Extremely low threshold to get everything
                iou=self.config.IOU_THRESHOLD,
                device=self.config.DEVICE,
                verbose=False
            )
            
            poses = []
            for result in results:
                boxes = result.boxes
                keypoints = result.keypoints
                
                for i in range(len(boxes)):
                    box = boxes[i]
                    cls = int(box.cls[0])
                    conf = float(box.conf[0])
                    xyxy = box.xyxy[0].cpu().numpy().tolist()
                    
                    # Get keypoints
                    kpts = []
                    if keypoints is not None and i < len(keypoints.data):
                        kpt_data = keypoints.data[i].cpu().numpy()
                        for j in range(len(kpt_data)):
                            kpts.append({
                                "x": float(kpt_data[j][0]),
                                "y": float(kpt_data[j][1]),
                                "confidence": float(kpt_data[j][2]) if len(kpt_data[j]) > 2 else 0.0
                            })
                    
                    poses.append({
                        "class": result.names[cls] if cls < len(result.names) else "person",
                        "class_id": cls,
                        "confidence": conf,
                        "bounding_box": {
                            "x1": float(xyxy[0]),
                            "y1": float(xyxy[1]),
                            "x2": float(xyxy[2]),
                            "y2": float(xyxy[3]),
                            "width": float(xyxy[2] - xyxy[0]),
                            "height": float(xyxy[3] - xyxy[1])
                        },
                        "keypoints": kpts,
                        "keypoint_count": len(kpts)
                    })
            
            return {
                "keypoints": poses,
                "count": len(poses)
            }
        except Exception as e:
            return {"error": str(e)}
    
    def classify(self, image_bytes: bytes) -> Dict[str, Any]:
        """Run image classification on image"""
        try:
            image = self._image_from_bytes(image_bytes)
            model = self.models.get('classify')
            
            if model is None:
                return {"error": "Classification model not loaded"}
            
            results = model.predict(
                image,
                device=self.config.DEVICE,
                verbose=False
            )
            
            classifications = []
            for result in results:
                probs = result.probs
                if probs is not None:
                    top5_indices = probs.top5
                    top5_probs = probs.top5conf.cpu().numpy()
                    
                    for idx, prob in zip(top5_indices, top5_probs):
                        classifications.append({
                            "class": result.names[int(idx)],
                            "class_id": int(idx),
                            "confidence": float(prob)
                        })
            
            top_class = classifications[0] if classifications else None
            
            return {
                "classes": classifications,
                "top": top_class,
                "count": len(classifications)
            }
        except Exception as e:
            return {"error": str(e)}
    
    def analyze_all(self, image_bytes: bytes, context: Optional[str] = None) -> Dict[str, Any]:
        """Run all YOLO analyses on image with optional context"""
        # Get image info once
        image = self._image_from_bytes(image_bytes)
        img_width, img_height = image.size
        
        result = {
            "detection": self.detect(image_bytes),
            "segmentation": self.segment(image_bytes),
            "pose": self.pose(image_bytes),
            "classification": self.classify(image_bytes),
            "image_info": {
                "width": img_width,
                "height": img_height,
                "format": image.format or "unknown"
            },
            "context": {
                "provided": context is not None and len(context.strip()) > 0,
                "description": context.strip() if context else "No context provided"
            },
            "model_info": {
                "detection_model": "loaded" if self.models.get('detect') else "not loaded",
                "segmentation_model": "loaded" if self.models.get('segment') else "not loaded",
                "pose_model": "loaded" if self.models.get('pose') else "not loaded",
                "classification_model": "loaded" if self.models.get('classify') else "not loaded",
                "note": "YOLO models are trained on general objects (COCO dataset). For construction/technical drawings, results may be limited. Context helps interpret results."
            }
        }
        return result


