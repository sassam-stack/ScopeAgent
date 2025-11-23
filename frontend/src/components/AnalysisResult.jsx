import { useState } from 'react'
import './AnalysisResult.css'

function AnalysisResult({ result }) {
  const [viewMode, setViewMode] = useState('formatted') // 'formatted' or 'json'
  const [copySuccess, setCopySuccess] = useState(false)

  if (!result) {
    return null
  }

  const handleCopyJson = async () => {
    try {
      const jsonString = JSON.stringify(result, null, 2)
      await navigator.clipboard.writeText(jsonString)
      setCopySuccess(true)
      setTimeout(() => setCopySuccess(false), 2000)
    } catch (err) {
      console.error('Failed to copy JSON:', err)
      // Fallback for older browsers
      const textArea = document.createElement('textarea')
      textArea.value = JSON.stringify(result, null, 2)
      document.body.appendChild(textArea)
      textArea.select()
      try {
        document.execCommand('copy')
        setCopySuccess(true)
        setTimeout(() => setCopySuccess(false), 2000)
      } catch (fallbackErr) {
        console.error('Fallback copy failed:', fallbackErr)
      }
      document.body.removeChild(textArea)
    }
  }

  return (
    <div className="analysis-result">
      <div className="result-header">
        <div className="header-top">
          <h2>Computer Vision Analysis Results</h2>
          <div className="header-actions">
            <div className="view-toggle">
              <button
                className={`toggle-button ${viewMode === 'formatted' ? 'active' : ''}`}
                onClick={() => setViewMode('formatted')}
              >
                Formatted
              </button>
              <button
                className={`toggle-button ${viewMode === 'json' ? 'active' : ''}`}
                onClick={() => setViewMode('json')}
              >
                Raw JSON
              </button>
            </div>
            <button
              className="copy-button"
              onClick={handleCopyJson}
              title="Copy JSON to clipboard"
            >
              {copySuccess ? '✓ Copied!' : 'Copy JSON'}
            </button>
          </div>
        </div>
        {result.modelVersion && (
          <div className="model-version">
            <strong>Model Version:</strong> {result.modelVersion}
          </div>
        )}
      </div>

      <div className="result-content">
        {viewMode === 'json' ? (
          <pre className="json-view">{JSON.stringify(result, null, 2)}</pre>
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

            {result.yolo && (
              <div className="yolo-section">
                <h3>YOLO Analysis Results</h3>
                
                {result.yolo.detection && !result.yolo.detection.error && (
                  <div className="yolo-detection">
                    <h4>Object Detection ({result.yolo.detection.count || 0} objects)</h4>
                    {result.yolo.detection.objects && result.yolo.detection.objects.length > 0 && (
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
                    )}
                  </div>
                )}

                {result.yolo.segmentation && !result.yolo.segmentation.error && (
                  <div className="yolo-segmentation">
                    <h4>Instance Segmentation ({result.yolo.segmentation.count || 0} segments)</h4>
                    {result.yolo.segmentation.masks && result.yolo.segmentation.masks.length > 0 && (
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
                    )}
                  </div>
                )}

                {result.yolo.pose && !result.yolo.pose.error && (
                  <div className="yolo-pose">
                    <h4>Pose Estimation ({result.yolo.pose.count || 0} poses)</h4>
                    {result.yolo.pose.keypoints && result.yolo.pose.keypoints.length > 0 && (
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
                    )}
                  </div>
                )}

                {result.yolo.classification && !result.yolo.classification.error && (
                  <div className="yolo-classification">
                    <h4>Image Classification</h4>
                    {result.yolo.classification.top && (
                      <div className="yolo-top-class">
                        <p><strong>Top Classification:</strong> {result.yolo.classification.top.class}</p>
                        <p><strong>Confidence:</strong> {(result.yolo.classification.top.confidence * 100).toFixed(2)}%</p>
                      </div>
                    )}
                    {result.yolo.classification.classes && result.yolo.classification.classes.length > 0 && (
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
                    )}
                  </div>
                )}

                {result.yolo.detection?.error && result.yolo.segmentation?.error && 
                 result.yolo.pose?.error && result.yolo.classification?.error && (
                  <div className="yolo-error">
                    <p>YOLO analysis unavailable: {result.yolo.detection.error || result.yolo.segmentation.error || result.yolo.pose.error || result.yolo.classification.error}</p>
                  </div>
                )}
              </div>
            )}

            {!result.caption && !result.denseCaptions && !result.objects && !result.tags && !result.categories && !result.color && !result.text && !result.yolo && (
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
