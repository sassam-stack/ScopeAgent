import { useState, useRef } from 'react'
import axios from 'axios'
import AnalysisResult from './AnalysisResult'
import './DocumentAnalyzer.css'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'

function DocumentAnalyzer() {
  const [isLoading, setIsLoading] = useState(false)
  const [result, setResult] = useState(null)
  const [error, setError] = useState(null)
  const [selectedFile, setSelectedFile] = useState(null)
  const [useComputerVision, setUseComputerVision] = useState(true)
  const [useYolo, setUseYolo] = useState(true)
  const [imageContext, setImageContext] = useState('')
  const fileInputRef = useRef(null)

  const handleFileSelect = async (file) => {
    if (!file || isLoading) return

    setSelectedFile(file)
    setIsLoading(true)
    setResult(null)
    setError(null)

    try {
      const formData = new FormData()
      formData.append('file', file)
      formData.append('useComputerVision', useComputerVision)
      formData.append('useYolo', useYolo)
      if (imageContext.trim()) {
        formData.append('context', imageContext.trim())
      }

      const response = await axios.post(`${API_BASE_URL}/analysis/analyze`, formData, {
        headers: {
          'Content-Type': 'multipart/form-data'
        }
      })

      setResult(response.data)
    } catch (err) {
      setError(err.response?.data || { 
        message: err.message || 'Failed to analyze image',
        error: err.toString()
      })
    } finally {
      setIsLoading(false)
    }
  }

  const handleFileChange = (e) => {
    const file = e.target.files[0]
    if (file) {
      handleFileSelect(file)
    }
  }

  const handleButtonClick = () => {
    fileInputRef.current?.click()
  }

  return (
    <div className="document-analyzer">
      <div className="analyzer-container">
        <div className="url-input-section">
          <div className="input-group">
            <label htmlFor="image-file">Image File:</label>
            <input
              ref={fileInputRef}
              id="image-file"
              type="file"
              accept="image/*"
              onChange={handleFileChange}
              style={{ display: 'none' }}
              disabled={isLoading}
            />
            <button
              onClick={handleButtonClick}
              disabled={isLoading}
              className="analyze-button"
            >
              {isLoading ? 'Analyzing...' : 'Select Image'}
            </button>
            {selectedFile && (
              <span className="file-name" style={{ marginLeft: '10px', color: '#666' }}>
                {selectedFile.name} ({(selectedFile.size / 1024).toFixed(2)} KB)
              </span>
            )}
          </div>
          <div className="service-selection">
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={useComputerVision}
                onChange={(e) => setUseComputerVision(e.target.checked)}
                disabled={isLoading}
              />
              <span>Azure Computer Vision</span>
            </label>
            <label className="checkbox-label" title="YOLO is best for photos with people, vehicles, animals. Not suitable for construction plans or technical drawings.">
              <input
                type="checkbox"
                checked={useYolo}
                onChange={(e) => setUseYolo(e.target.checked)}
                disabled={isLoading}
              />
              <span>YOLO Analysis <span style={{ fontSize: '0.85em', color: '#999' }}>(general objects only)</span></span>
            </label>
          </div>
          <div className="input-group" style={{ marginTop: '16px' }}>
            <label htmlFor="image-context">Image Context (optional):</label>
            <input
              id="image-context"
              type="text"
              value={imageContext}
              onChange={(e) => setImageContext(e.target.value)}
              placeholder="e.g., Construction drainage plan, technical drawing, architectural blueprint, etc."
              disabled={isLoading}
              style={{
                width: '100%',
                padding: '12px 16px',
                border: '2px solid #e0e0e0',
                borderRadius: '6px',
                fontSize: '14px',
                marginTop: '8px'
              }}
            />
            <div className="input-hint" style={{ marginTop: '4px' }}>
              Provide context about the image to help interpret YOLO results. Context is also automatically extracted from Computer Vision analysis.
            </div>
          </div>
          <div className="input-hint">
            Supported formats: JPG, PNG, GIF, BMP, WebP
          </div>
        </div>

        {isLoading && (
          <div className="loading-section">
            <div className="spinner"></div>
            <span>
              Analyzing image...
              {useComputerVision && useYolo && ' (Computer Vision & YOLO)'}
              {useComputerVision && !useYolo && ' (Computer Vision)'}
              {!useComputerVision && useYolo && ' (YOLO)'}
            </span>
          </div>
        )}

        {error && (
          <div className="error-section">
            <h3>Error</h3>
            <pre className="result-json">{JSON.stringify(error, null, 2)}</pre>
          </div>
        )}

        {result && (
          <div className="result-section">
            <AnalysisResult result={result} />
          </div>
        )}
      </div>
    </div>
  )
}

export default DocumentAnalyzer
