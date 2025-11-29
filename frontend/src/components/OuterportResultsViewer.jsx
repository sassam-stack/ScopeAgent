import { useState, useEffect, useRef } from 'react'
import './OuterportResultsViewer.css'

function OuterportResultsViewer({ imageUrl, results, onClose }) {
  const canvasRef = useRef(null)
  const imageRef = useRef(null)
  const containerRef = useRef(null)
  const [imageLoaded, setImageLoaded] = useState(false)
  const [scale, setScale] = useState(1)
  const [imageSize, setImageSize] = useState({ width: 0, height: 0 })

  useEffect(() => {
    if (imageUrl && results) {
      const img = new Image()
      img.onload = () => {
        setImageSize({ width: img.naturalWidth, height: img.naturalHeight })
        setImageLoaded(true)
        // Delay to ensure canvas is ready
        setTimeout(() => drawBoxes(), 100)
      }
      img.src = imageUrl
      imageRef.current = img
    }
  }, [imageUrl, results])

  useEffect(() => {
    if (imageLoaded && canvasRef.current) {
      drawBoxes()
    }
  }, [imageLoaded])

  const drawBoxes = () => {
    const canvas = canvasRef.current
    const img = imageRef.current
    if (!canvas || !imageLoaded || !results || !img) return

    // Set canvas size to match image natural size
    canvas.width = img.naturalWidth
    canvas.height = img.naturalHeight

    const ctx = canvas.getContext('2d')
    ctx.clearRect(0, 0, canvas.width, canvas.height)

    // Draw junctions (modules)
    if (results.junctions && results.junctions.length > 0) {
      results.junctions.forEach((junction) => {
        // Draw label bbox in red (handle both camelCase and snake_case)
        const labelBbox = junction.labelBbox || junction.label_bbox
        if (labelBbox && labelBbox.length === 4) {
          const [x1, y1, x2, y2] = labelBbox
          ctx.strokeStyle = 'red'
          ctx.lineWidth = 3
          ctx.strokeRect(x1, y1, x2 - x1, y2 - y1)
          
          // Draw junction ID text background
          ctx.fillStyle = 'rgba(255, 0, 0, 0.7)'
          ctx.fillRect(x1, y1 - 18, 60, 16)
          
          // Draw junction ID text
          ctx.fillStyle = 'white'
          ctx.font = 'bold 12px Arial'
          ctx.fillText(junction.id || '', x1 + 2, y1 - 4)
        }

        // Draw module bbox in blue
        if (junction.bbox && junction.bbox.length === 4) {
          const [x1, y1, x2, y2] = junction.bbox
          ctx.strokeStyle = 'blue'
          ctx.lineWidth = 3
          ctx.strokeRect(x1, y1, x2 - x1, y2 - y1)
        }
      })
    }

    // Draw materials in yellow
    if (results.materials && results.materials.length > 0) {
      results.materials.forEach((material) => {
        if (material.bbox && material.bbox.length === 4) {
          const [x1, y1, x2, y2] = material.bbox
          ctx.strokeStyle = 'yellow'
          ctx.lineWidth = 2
          ctx.strokeRect(x1, y1, x2 - x1, y2 - y1)
          
          // Draw material text background
          if (material.text) {
            const textWidth = ctx.measureText(material.text).width
            ctx.fillStyle = 'rgba(255, 255, 0, 0.7)'
            ctx.fillRect(x1, y1 - 16, textWidth + 4, 14)
            
            // Draw material text
            ctx.fillStyle = 'black'
            ctx.font = 'bold 11px Arial'
            ctx.fillText(material.text, x1 + 2, y1 - 3)
          }
        }
      })
    }
  }

  const handleImageLoad = (e) => {
    const img = e.target
    setImageSize({ width: img.naturalWidth, height: img.naturalHeight })
    setImageLoaded(true)
    setTimeout(() => drawBoxes(), 100)
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content outerport-modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>Outerport Results</h3>
          <button className="modal-close" onClick={onClose}>×</button>
        </div>
        <div className="modal-body">
          {results && (
            <div className="outerport-results-container">
              <div className="outerport-stats">
                <div className="stat-item">
                  <span className="stat-label">Junctions:</span>
                  <span className="stat-value">{results.junctions?.length || 0}</span>
                </div>
                <div className="stat-item">
                  <span className="stat-label">Materials:</span>
                  <span className="stat-value">{results.materials?.length || 0}</span>
                </div>
              </div>
              
              <div className="legend">
                <div className="legend-item">
                  <div className="legend-color" style={{ backgroundColor: 'red' }}></div>
                  <span>Label (Module Name)</span>
                </div>
                <div className="legend-item">
                  <div className="legend-color" style={{ backgroundColor: 'blue' }}></div>
                  <span>Module Symbol</span>
                </div>
                <div className="legend-item">
                  <div className="legend-color" style={{ backgroundColor: 'yellow' }}></div>
                  <span>Material/Pipe</span>
                </div>
              </div>

              {imageUrl && (
                <div className="outerport-image-container" ref={containerRef}>
                  <div className="image-wrapper" style={{ position: 'relative', display: 'inline-block' }}>
                    <img
                      ref={imageRef}
                      src={imageUrl}
                      alt="Plan with highlights"
                      style={{ maxWidth: '100%', height: 'auto', display: 'block' }}
                      onLoad={handleImageLoad}
                    />
                    <canvas
                      ref={canvasRef}
                      style={{
                        position: 'absolute',
                        top: 0,
                        left: 0,
                        pointerEvents: 'none',
                        width: '100%',
                        height: '100%'
                      }}
                    />
                  </div>
                </div>
              )}

              <div className="outerport-results-list">
                {results.junctions && results.junctions.length > 0 && (
                  <div className="junctions-section">
                    <h4>Junctions ({results.junctions.length})</h4>
                    <div className="junctions-list">
                      {results.junctions.map((junction, idx) => (
                        <div key={idx} className="junction-item">
                          <div className="junction-id">
                            <strong>ID:</strong> {junction.id || 'N/A'}
                          </div>
                          {(junction.labelBbox || junction.label_bbox) && (junction.labelBbox || junction.label_bbox).length === 4 && (
                            <div className="junction-detail">
                              <strong>Label Box:</strong> [{(junction.labelBbox || junction.label_bbox).join(', ')}]
                            </div>
                          )}
                          {junction.bbox && junction.bbox.length === 4 && (
                            <div className="junction-detail">
                              <strong>Module Box:</strong> [{junction.bbox.join(', ')}]
                            </div>
                          )}
                          {(junction.expectedDirections || junction.expected_directions) && (junction.expectedDirections || junction.expected_directions).length > 0 && (
                            <div className="junction-detail">
                              <strong>Expected Directions:</strong> {(junction.expectedDirections || junction.expected_directions).join(', ')}
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {results.materials && results.materials.length > 0 && (
                  <div className="materials-section">
                    <h4>Materials ({results.materials.length})</h4>
                    <div className="materials-list">
                      {results.materials.map((material, idx) => (
                        <div key={idx} className="material-item">
                          <div className="material-text">
                            <strong>Text:</strong> {material.text || 'N/A'}
                          </div>
                          {material.bbox && material.bbox.length === 4 && (
                            <div className="material-detail">
                              <strong>Box:</strong> [{material.bbox.join(', ')}]
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                <details className="outerport-json-details">
                  <summary>View Full JSON Data</summary>
                  <pre className="outerport-json">{JSON.stringify(results, null, 2)}</pre>
                </details>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

export default OuterportResultsViewer

