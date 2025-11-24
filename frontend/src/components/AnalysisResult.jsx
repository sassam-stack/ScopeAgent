import { useState } from 'react'
import './AnalysisResult.css'

function AnalysisResult({ result }) {
  const [activeTab, setActiveTab] = useState('computerVision') // 'computerVision', 'cvJson', 'yolo', 'yoloJson'
  const [copySuccess, setCopySuccess] = useState(null) // 'cv' or 'yolo' or null

  if (!result) {
    return null
  }

  // Extract Computer Vision data (everything except yolo)
  const getComputerVisionData = () => {
    const cvData = { ...result }
    delete cvData.yolo
    return cvData
  }

  // Extract YOLO data
  const getYoloData = () => {
    return result.yolo || {}
  }

  const handleCopyJson = async (type) => {
    try {
      const jsonString = type === 'cv' 
        ? JSON.stringify(getComputerVisionData(), null, 2)
        : JSON.stringify(getYoloData(), null, 2)
      
      await navigator.clipboard.writeText(jsonString)
      setCopySuccess(type)
      setTimeout(() => setCopySuccess(null), 2000)
    } catch (err) {
      console.error('Failed to copy JSON:', err)
      // Fallback for older browsers
      const textArea = document.createElement('textarea')
      const jsonString = type === 'cv' 
        ? JSON.stringify(getComputerVisionData(), null, 2)
        : JSON.stringify(getYoloData(), null, 2)
      textArea.value = jsonString
      document.body.appendChild(textArea)
      textArea.select()
      try {
        document.execCommand('copy')
        setCopySuccess(type)
        setTimeout(() => setCopySuccess(null), 2000)
      } catch (fallbackErr) {
        console.error('Fallback copy failed:', fallbackErr)
      }
      document.body.removeChild(textArea)
    }
  }

  // Check if YOLO results exist
  const hasYoloResults = result.yolo && (
    (result.yolo.detection && !result.yolo.detection.error) ||
    (result.yolo.segmentation && !result.yolo.segmentation.error) ||
    (result.yolo.pose && !result.yolo.pose.error) ||
    (result.yolo.classification && !result.yolo.classification.error)
  )

  // Check if Computer Vision results exist
  const hasComputerVisionResults = result && Object.keys(getComputerVisionData()).length > 0

  return (
    <div className="analysis-result">
      <div className="result-header">
        <div className="header-top">
          <h2>Analysis Results</h2>
        </div>
        {result.modelVersion && (
          <div className="model-version">
            <strong>Model Version:</strong> {result.modelVersion}
          </div>
        )}
        <div className="tabs-container">
          {hasComputerVisionResults && (
            <button
              className={`tab-button ${activeTab === 'computerVision' ? 'active' : ''}`}
              onClick={() => setActiveTab('computerVision')}
            >
              Computer Vision
            </button>
          )}
          {hasComputerVisionResults && (
            <button
              className={`tab-button ${activeTab === 'cvJson' ? 'active' : ''}`}
              onClick={() => setActiveTab('cvJson')}
            >
              CV Raw JSON
            </button>
          )}
          {hasYoloResults && (
            <button
              className={`tab-button ${activeTab === 'yolo' ? 'active' : ''}`}
              onClick={() => setActiveTab('yolo')}
            >
              YOLO Analysis
            </button>
          )}
          {hasYoloResults && (
            <button
              className={`tab-button ${activeTab === 'yoloJson' ? 'active' : ''}`}
              onClick={() => setActiveTab('yoloJson')}
            >
              YOLO Raw JSON
            </button>
          )}
        </div>
      </div>

      <div className="result-content">
        {activeTab === 'cvJson' ? (
          <div className="json-tab-container">
            <div className="json-tab-header">
              <button
                className="copy-button"
                onClick={() => handleCopyJson('cv')}
                title="Copy CV JSON to clipboard"
              >
                {copySuccess === 'cv' ? '✓ Copied!' : 'Copy JSON'}
              </button>
            </div>
            <pre className="json-view">{JSON.stringify(getComputerVisionData(), null, 2)}</pre>
          </div>
        ) : activeTab === 'yoloJson' ? (
          <div className="json-tab-container">
            <div className="json-tab-header">
              <button
                className="copy-button"
                onClick={() => handleCopyJson('yolo')}
                title="Copy YOLO JSON to clipboard"
              >
                {copySuccess === 'yolo' ? '✓ Copied!' : 'Copy JSON'}
              </button>
            </div>
            <pre className="json-view">{JSON.stringify(getYoloData(), null, 2)}</pre>
          </div>
        ) : activeTab === 'yolo' ? (
          <div className="formatted-view">
            {result.yolo && (
              <div className="yolo-section">
                <h3>YOLO Analysis Results</h3>
                <div className="yolo-warning" style={{ marginBottom: '24px' }}>
                  <p><strong>⚠️ Limitation Notice:</strong> YOLO is designed for general object detection (people, vehicles, animals, etc.) and is <strong>not suitable</strong> for construction plans, technical drawings, or architectural blueprints.</p>
                  <p>If you're analyzing a construction/technical drawing, please check the <strong>Computer Vision</strong> tab for more relevant results.</p>
                </div>
                
                {result.yolo.image_info && (
                  <div className="yolo-image-info">
                    <p><strong>Image:</strong> {result.yolo.image_info.width}x{result.yolo.image_info.height}px 
                      {result.yolo.image_info.format && ` (${result.yolo.image_info.format})`}</p>
                  </div>
                )}
                
                {result.yolo.context && (
                  <div className="yolo-context">
                    <h4>Image Context</h4>
                    {result.yolo.context.provided ? (
                      <div className="context-content">
                        <p>{result.yolo.context.description}</p>
                        <div className="yolo-warning">
                          <p><strong>⚠️ Important:</strong> YOLO models are trained on general objects (people, cars, animals, etc.) from the COCO dataset. They <strong>cannot recognize</strong> construction plans, technical drawings, drainage systems, or architectural blueprints.</p>
                          <p>For construction/technical drawings, <strong>Azure Computer Vision results are much more useful</strong> as they can understand document structure, extract text, and provide relevant descriptions.</p>
                          <p>YOLO results shown here are likely irrelevant for this image type. Consider focusing on the Computer Vision tab instead.</p>
                        </div>
                      </div>
                    ) : (
                      <div className="yolo-warning">
                        <p><strong>⚠️ Important:</strong> YOLO models are trained on general objects and may not recognize specialized content like construction plans.</p>
                      </div>
                    )}
                  </div>
                )}
                
                {result.yolo.detection && !result.yolo.detection.error && (
                  <div className="yolo-detection">
                    <h4>Object Detection ({result.yolo.detection.count || 0} objects)</h4>
                    {result.yolo.detection.config && (
                      <div className="detection-config">
                        <p><strong>Confidence Threshold:</strong> {result.yolo.detection.config.confidence_threshold}</p>
                        <p><strong>IOU Threshold:</strong> {result.yolo.detection.config.iou_threshold}</p>
                        {result.yolo.detection.config.note && (
                          <p><em>{result.yolo.detection.config.note}</em></p>
                        )}
                      </div>
                    )}
                    {result.yolo.detection.objects && result.yolo.detection.objects.length > 0 ? (
                      <div className="yolo-objects-list">
                        {result.yolo.detection.objects.map((obj, index) => (
                          <div key={index} className="yolo-object-item">
                            <p><strong>Object {index + 1}:</strong> {obj.class}</p>
                            <p><strong>Confidence:</strong> {(obj.confidence * 100).toFixed(2)}%</p>
                            {obj.bounding_box && (
                              <p><strong>Bounding Box:</strong> 
                                x1={obj.bounding_box.x1.toFixed(0)}, y1={obj.bounding_box.y1.toFixed(0)}, 
                                x2={obj.bounding_box.x2.toFixed(0)}, y2={obj.bounding_box.y2.toFixed(0)}
                                <br />
                                Size: {obj.bounding_box.width.toFixed(0)}x{obj.bounding_box.height.toFixed(0)}px
                              </p>
                            )}
                          </div>
                        ))}
                      </div>
                    ) : (
                      <p className="no-items">No objects detected</p>
                    )}
                  </div>
                )}

                {result.yolo.segmentation && !result.yolo.segmentation.error && (
                  <div className="yolo-segmentation">
                    <h4>Instance Segmentation ({result.yolo.segmentation.count || 0} segments)</h4>
                    {result.yolo.segmentation.masks && result.yolo.segmentation.masks.length > 0 ? (
                      <div className="yolo-segments-list">
                        {result.yolo.segmentation.masks.map((mask, index) => (
                          <div key={index} className="yolo-segment-item">
                            <p><strong>Segment {index + 1}:</strong> {mask.class}</p>
                            <p><strong>Confidence:</strong> {(mask.confidence * 100).toFixed(2)}%</p>
                            {mask.bounding_box && (
                              <p><strong>Bounding Box:</strong> 
                                x1={mask.bounding_box.x1.toFixed(0)}, y1={mask.bounding_box.y1.toFixed(0)}, 
                                x2={mask.bounding_box.x2.toFixed(0)}, y2={mask.bounding_box.y2.toFixed(0)}
                              </p>
                            )}
                            {mask.mask_points && (
                              <p><strong>Mask Points:</strong> {mask.mask_points.length} points</p>
                            )}
                            {mask.mask_area && (
                              <p><strong>Mask Area:</strong> {mask.mask_area.toFixed(0)} pixels</p>
                            )}
                          </div>
                        ))}
                      </div>
                    ) : (
                      <p className="no-items">No segments detected</p>
                    )}
                  </div>
                )}

                {result.yolo.pose && !result.yolo.pose.error && (
                  <div className="yolo-pose">
                    <h4>Pose Estimation ({result.yolo.pose.count || 0} poses)</h4>
                    {result.yolo.pose.keypoints && result.yolo.pose.keypoints.length > 0 ? (
                      <div className="yolo-poses-list">
                        {result.yolo.pose.keypoints.map((pose, index) => (
                          <div key={index} className="yolo-pose-item">
                            <p><strong>Pose {index + 1}:</strong> {pose.class}</p>
                            <p><strong>Confidence:</strong> {(pose.confidence * 100).toFixed(2)}%</p>
                            {pose.bounding_box && (
                              <p><strong>Bounding Box:</strong> 
                                x1={pose.bounding_box.x1.toFixed(0)}, y1={pose.bounding_box.y1.toFixed(0)}, 
                                x2={pose.bounding_box.x2.toFixed(0)}, y2={pose.bounding_box.y2.toFixed(0)}
                              </p>
                            )}
                            {pose.keypoints && pose.keypoints.length > 0 && (
                              <div className="yolo-keypoints">
                                <strong>Keypoints ({pose.keypoint_count || pose.keypoints.length}):</strong>
                                <div className="keypoints-list">
                                  {pose.keypoints.map((kp, kpIndex) => (
                                    <span key={kpIndex} className="keypoint-item">
                                      KP{kpIndex + 1}: ({kp.x.toFixed(0)}, {kp.y.toFixed(0)}) 
                                      {kp.confidence ? ` ${(kp.confidence * 100).toFixed(0)}%` : ''}
                                    </span>
                                  ))}
                                </div>
                              </div>
                            )}
                          </div>
                        ))}
                      </div>
                    ) : (
                      <p className="no-items">No poses detected</p>
                    )}
                  </div>
                )}

                {result.yolo.classification && !result.yolo.classification.error && (
                  <div className="yolo-classification">
                    <h4>Image Classification</h4>
                    <div className="classification-note">
                      <p><em>Note: Classification uses ImageNet classes (general objects). Results may not be relevant for technical drawings or construction plans.</em></p>
                    </div>
                    {result.yolo.classification.top && (
                      <div className="yolo-top-class">
                        <p><strong>Top Classification:</strong> {result.yolo.classification.top.class}</p>
                        <p><strong>Confidence:</strong> {(result.yolo.classification.top.confidence * 100).toFixed(2)}%</p>
                      </div>
                    )}
                    {result.yolo.classification.classes && result.yolo.classification.classes.length > 0 ? (
                      <div className="yolo-classes-list">
                        <strong>Top {result.yolo.classification.classes.length} Classes:</strong>
                        <ul>
                          {result.yolo.classification.classes.map((cls, index) => (
                            <li key={index}>
                              {cls.class}: {(cls.confidence * 100).toFixed(2)}%
                            </li>
                          ))}
                        </ul>
                      </div>
                    ) : (
                      <p className="no-items">No classifications available</p>
                    )}
                  </div>
                )}

                {result.yolo.detection?.error && result.yolo.segmentation?.error && 
                 result.yolo.pose?.error && result.yolo.classification?.error && (
                  <div className="yolo-error">
                    <p>YOLO analysis unavailable: {result.yolo.detection.error || result.yolo.segmentation.error || result.yolo.pose.error || result.yolo.classification.error}</p>
                  </div>
                )}
                
                {result.yolo.model_info && result.yolo.model_info.note && (
                  <div className="yolo-model-note">
                    <p><strong>Note:</strong> {result.yolo.model_info.note}</p>
                  </div>
                )}
              </div>
            )}
            {!result.yolo && (
              <div className="no-results">
                No YOLO analysis results available.
              </div>
            )}
          </div>
        ) : (
          <div className="formatted-view">
            {(result.caption || (result.captions && result.captions.length > 0)) && (
              <div className="caption-section">
                <h3>Image Caption</h3>
                {result.caption ? (
                  <div className="caption-content">
                    <p><strong>Text:</strong> {result.caption.text}</p>
                    <p><strong>Confidence:</strong> {(result.caption.confidence * 100).toFixed(2)}%</p>
                  </div>
                ) : null}
                {result.captions && result.captions.length > 0 && (
                  <div className="captions-list">
                    {result.captions.map((caption, index) => (
                      <div key={index} className="caption-item">
                        <p><strong>Caption {index + 1}:</strong> {caption.text}</p>
                        <p><strong>Confidence:</strong> {(caption.confidence * 100).toFixed(2)}%</p>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}

            {result.denseCaptions && result.denseCaptions.length > 0 && (
              <div className="dense-captions-section">
                <h3>Dense Captions</h3>
                <div className="dense-captions-list">
                  {result.denseCaptions.map((caption, index) => (
                    <div key={index} className="dense-caption-item">
                      <p><strong>Text:</strong> {caption.text}</p>
                      <p><strong>Confidence:</strong> {(caption.confidence * 100).toFixed(2)}%</p>
                      {caption.boundingBox && (
                        <p><strong>Position:</strong> x={caption.boundingBox.x}, y={caption.boundingBox.y}, 
                           width={caption.boundingBox.width}, height={caption.boundingBox.height}</p>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {result.objects && result.objects.length > 0 && (
              <div className="objects-section">
                <h3>Detected Objects</h3>
                <div className="objects-list">
                  {result.objects.map((obj, index) => (
                    <div key={index} className="object-item">
                      <p><strong>Name:</strong> {obj.name}</p>
                      <p><strong>Confidence:</strong> {(obj.confidence * 100).toFixed(2)}%</p>
                      {obj.boundingBox && (
                        <p><strong>Position:</strong> x={obj.boundingBox.x}, y={obj.boundingBox.y}, 
                           width={obj.boundingBox.width}, height={obj.boundingBox.height}</p>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {result.tags && result.tags.length > 0 && (
              <div className="tags-section">
                <h3>Image Tags</h3>
                <div className="tags-list">
                  {result.tags.map((tag, index) => (
                    <span key={index} className="tag-badge">
                      {tag.name} ({(tag.confidence * 100).toFixed(0)}%)
                    </span>
                  ))}
                </div>
              </div>
            )}

            {result.categories && result.categories.length > 0 && (
              <div className="categories-section">
                <h3>Image Categories</h3>
                <div className="categories-list">
                  {result.categories.map((category, index) => (
                    <div key={index} className="category-item">
                      <p><strong>Category:</strong> {category.name}</p>
                      <p><strong>Score:</strong> {(category.score * 100).toFixed(2)}%</p>
                      {category.detail && (
                        <div className="category-details">
                          {category.detail.celebrities && category.detail.celebrities.length > 0 && (
                            <div>
                              <strong>Celebrities:</strong>
                              <ul>
                                {category.detail.celebrities.map((celeb, i) => (
                                  <li key={i}>{celeb.name} ({(celeb.confidence * 100).toFixed(0)}%)</li>
                                ))}
                              </ul>
                            </div>
                          )}
                          {category.detail.landmarks && category.detail.landmarks.length > 0 && (
                            <div>
                              <strong>Landmarks:</strong>
                              <ul>
                                {category.detail.landmarks.map((landmark, i) => (
                                  <li key={i}>{landmark.name} ({(landmark.confidence * 100).toFixed(0)}%)</li>
                                ))}
                              </ul>
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {result.color && (
              <div className="color-section">
                <h3>Color Analysis</h3>
                <div className="color-info">
                  {result.color.dominantColorForeground && (
                    <p><strong>Dominant Foreground Color:</strong> {result.color.dominantColorForeground}</p>
                  )}
                  {result.color.dominantColorBackground && (
                    <p><strong>Dominant Background Color:</strong> {result.color.dominantColorBackground}</p>
                  )}
                  {result.color.dominantColors && result.color.dominantColors.length > 0 && (
                    <div>
                      <strong>Dominant Colors:</strong>
                      <div className="colors-list">
                        {result.color.dominantColors.map((color, index) => (
                          <span key={index} className="color-badge" style={{ backgroundColor: color, color: '#fff', padding: '2px 8px', margin: '2px', borderRadius: '4px' }}>
                            {color}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}
                  {result.color.accentColor && (
                    <p><strong>Accent Color:</strong> 
                      <span className="color-badge" style={{ backgroundColor: result.color.accentColor, color: '#fff', padding: '2px 8px', marginLeft: '8px', borderRadius: '4px' }}>
                        {result.color.accentColor}
                      </span>
                    </p>
                  )}
                  {result.color.isBlackAndWhite !== undefined && (
                    <p><strong>Black and White:</strong> {result.color.isBlackAndWhite ? 'Yes' : 'No'}</p>
                  )}
                </div>
              </div>
            )}

            {result.text && (
              <div className="text-section">
                <h3>Extracted Text (OCR)</h3>
                {result.text.content && (
                  <div className="text-content">
                    <h4>Full Text Content:</h4>
                    <pre style={{ whiteSpace: 'pre-wrap', wordWrap: 'break-word' }}>{result.text.content}</pre>
                  </div>
                )}
                {result.text.pages && result.text.pages.length > 0 && (
                  <div className="text-pages">
                    <h4>Text by Page ({result.text.pages.length} page(s))</h4>
                    {result.text.pages.map((page, pageIndex) => (
                      <div key={pageIndex} className="text-page">
                        <h5>Page {page.pageNumber} ({page.width}x{page.height}px)</h5>
                        {page.lines && page.lines.length > 0 && (
                          <div className="text-lines">
                            {page.lines.map((line, lineIndex) => (
                              <div key={lineIndex} className="text-line">
                                <p><strong>Line {lineIndex + 1}:</strong> {line.text}</p>
                                {line.words && line.words.length > 0 && (
                                  <div className="words-in-line">
                                    <strong>Words:</strong>
                                    {line.words.map((word, wordIndex) => (
                                      <span key={wordIndex} className="word-item" style={{ marginLeft: '8px' }}>
                                        {word.text} ({(word.confidence * 100).toFixed(0)}%)
                                      </span>
                                    ))}
                                  </div>
                                )}
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}

            {!result.caption && !result.denseCaptions && !result.objects && !result.tags && !result.categories && !result.color && !result.text && (
              <div className="no-results">
                No analysis results available.
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

export default AnalysisResult
