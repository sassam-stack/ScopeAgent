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
          <div className="input-hint">
            Supported formats: JPG, PNG, GIF, BMP, WebP
          </div>
        </div>

        {isLoading && (
          <div className="loading-section">
            <div className="spinner"></div>
            <span>Analyzing image with Azure Computer Vision...</span>
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
