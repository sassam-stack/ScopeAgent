import { useState, useRef, useEffect } from 'react'
import axios from 'axios'
import './DrainagePlanAnalyzer.css'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'

function DrainagePlanAnalyzer() {
  const [selectedFile, setSelectedFile] = useState(null)
  const [planPageNumber, setPlanPageNumber] = useState(1)
  const [contentTablePage, setContentTablePage] = useState('')
  const [moduleList, setModuleList] = useState('')
  const [isUploading, setIsUploading] = useState(false)
  const [analysisId, setAnalysisId] = useState(null)
  const [status, setStatus] = useState(null)
  const [error, setError] = useState(null)
  const [showImageModal, setShowImageModal] = useState(false)
  const [showOCRModal, setShowOCRModal] = useState(false)
  const [imageModalContent, setImageModalContent] = useState(null)
  const [imageModalTitle, setImageModalTitle] = useState('')
  const [ocrData, setOcrData] = useState(null)
  const [loadingImage, setLoadingImage] = useState(false)
  const [loadingOCR, setLoadingOCR] = useState(false)
  const fileInputRef = useRef(null)

  // Poll for status updates
  useEffect(() => {
    if (!analysisId) return

    const pollStatus = async () => {
      try {
        const response = await axios.get(`${API_BASE_URL}/drainage/${analysisId}/status`)
        setStatus(response.data)
        
        // Stop polling if completed or error (handle both string and enum values)
        const statusValue = String(response.data.status || '').toLowerCase()
        if (statusValue === 'completed' || statusValue === '2' || 
            statusValue === 'error' || statusValue === '3') {
          return
        }
      } catch (err) {
        console.error('Error polling status:', err)
      }
    }

    // Poll immediately, then every 2 seconds
    pollStatus()
    const interval = setInterval(pollStatus, 2000)

    return () => clearInterval(interval)
  }, [analysisId])

  const handleFileSelect = (e) => {
    const file = e.target.files?.[0]
    if (file) {
      if (file.type !== 'application/pdf') {
        setError('Please select a PDF file')
        return
      }
      setSelectedFile(file)
      setError(null)
      setAnalysisId(null)
      setStatus(null)
    }
  }

  const handleUpload = async () => {
    if (!selectedFile || isUploading) return

    setIsUploading(true)
    setError(null)
    setStatus(null)

    try {
      const formData = new FormData()
      formData.append('pdfFile', selectedFile)
      formData.append('planPageNumber', planPageNumber.toString())
      
      if (contentTablePage) {
        formData.append('contentTablePageNumber', contentTablePage)
      }
      
      if (moduleList.trim()) {
        const modules = moduleList.split(',').map(m => m.trim()).filter(m => m)
        formData.append('moduleList', JSON.stringify(modules))
      }

      const response = await axios.post(`${API_BASE_URL}/drainage/upload`, formData, {
        headers: {
          'Content-Type': 'multipart/form-data'
        }
      })

      setAnalysisId(response.data.analysisId)
      setStatus({
        status: response.data.status,
        progress: 0,
        message: response.data.message
      })
    } catch (err) {
      setError(err.response?.data?.error || err.message || 'Failed to upload PDF')
    } finally {
      setIsUploading(false)
    }
  }

  const handleReset = () => {
    setSelectedFile(null)
    setAnalysisId(null)
    setStatus(null)
    setError(null)
    setPlanPageNumber(1)
    setContentTablePage('')
    setModuleList('')
    setShowImageModal(false)
    setShowOCRModal(false)
    setImageModalContent(null)
    setOcrData(null)
    if (fileInputRef.current) {
      fileInputRef.current.value = ''
    }
  }

  const handleViewPlanImage = async () => {
    if (!analysisId) return
    
    setLoadingImage(true)
    try {
      const response = await axios.get(`${API_BASE_URL}/drainage/${analysisId}/image/plan`, {
        responseType: 'blob'
      })
      const imageUrl = URL.createObjectURL(response.data)
      setImageModalContent(imageUrl)
      setImageModalTitle('Plan Page Image')
      setShowImageModal(true)
    } catch (err) {
      if (err.response?.status === 404) {
        alert('Plan page image not yet available. It will be generated during processing.')
      } else {
        alert(`Error loading image: ${err.response?.data?.error || err.message}`)
      }
    } finally {
      setLoadingImage(false)
    }
  }

  const handleViewContentTableImage = async () => {
    if (!analysisId) return
    
    setLoadingImage(true)
    try {
      const response = await axios.get(`${API_BASE_URL}/drainage/${analysisId}/image/content-table`, {
        responseType: 'blob'
      })
      const imageUrl = URL.createObjectURL(response.data)
      setImageModalContent(imageUrl)
      setImageModalTitle('Content Table Page Image')
      setShowImageModal(true)
    } catch (err) {
      if (err.response?.status === 404) {
        alert('Content table page image not available.')
      } else {
        alert(`Error loading image: ${err.response?.data?.error || err.message}`)
      }
    } finally {
      setLoadingImage(false)
    }
  }

  const handleViewOCRResults = async () => {
    if (!analysisId) return
    
    setLoadingOCR(true)
    try {
      const response = await axios.get(`${API_BASE_URL}/drainage/${analysisId}/ocr`)
      setOcrData(response.data)
      setShowOCRModal(true)
    } catch (err) {
      if (err.response?.status === 404) {
        alert('OCR results not yet available. They will be generated during processing.')
      } else {
        alert(`Error loading OCR results: ${err.response?.data?.error || err.message}`)
      }
    } finally {
      setLoadingOCR(false)
    }
  }

  const closeImageModal = () => {
    if (imageModalContent && imageModalContent.startsWith('blob:')) {
      URL.revokeObjectURL(imageModalContent)
    }
    setShowImageModal(false)
    setImageModalContent(null)
    setImageModalTitle('')
  }

  const closeOCRModal = () => {
    setShowOCRModal(false)
    setOcrData(null)
  }

  // Helper function to format status for display
  const formatStatus = (status) => {
    if (!status) return 'Unknown'
    const statusStr = String(status)
    // Handle enum values (numbers) or string values
    const statusMap = {
      '0': 'Processing',
      '1': 'Ready For Validation',
      '2': 'Completed',
      '3': 'Error',
      'Processing': 'Processing',
      'ReadyForValidation': 'Ready For Validation',
      'Completed': 'Completed',
      'Error': 'Error'
    }
    return statusMap[statusStr] || statusStr.replace(/_/g, ' ').replace(/([A-Z])/g, ' $1').trim()
  }

  return (
    <div className="drainage-analyzer">
      <div className="analyzer-container">
        <div className="upload-section">
          <h2>Drainage Plan Analyzer</h2>
          <p className="subtitle">Upload a PDF drainage plan for analysis</p>

          <div className="form-group">
            <label htmlFor="pdfFile">PDF File *</label>
            <input
              type="file"
              id="pdfFile"
              ref={fileInputRef}
              accept=".pdf"
              onChange={handleFileSelect}
              disabled={isUploading}
            />
            {selectedFile && (
              <div className="file-info">
                <span className="file-name">{selectedFile.name}</span>
                <span className="file-size">{(selectedFile.size / 1024 / 1024).toFixed(2)} MB</span>
              </div>
            )}
          </div>

          <div className="form-group">
            <label htmlFor="planPage">Plan Page Number *</label>
            <input
              type="number"
              id="planPage"
              min="1"
              value={planPageNumber}
              onChange={(e) => setPlanPageNumber(parseInt(e.target.value) || 1)}
              disabled={isUploading}
            />
          </div>

          <div className="form-group">
            <label htmlFor="contentTable">Content Table Page (Optional)</label>
            <input
              type="number"
              id="contentTable"
              min="1"
              value={contentTablePage}
              onChange={(e) => setContentTablePage(e.target.value)}
              disabled={isUploading}
              placeholder="Leave empty if not applicable"
            />
          </div>

          <div className="form-group">
            <label htmlFor="moduleList">Module List (Optional)</label>
            <input
              type="text"
              id="moduleList"
              value={moduleList}
              onChange={(e) => setModuleList(e.target.value)}
              disabled={isUploading}
              placeholder="e.g., S-1, S-2, S-3"
            />
            <small>Comma-separated list of module labels</small>
          </div>

          {error && (
            <div className="error-message">
              {error}
            </div>
          )}

          <div className="button-group">
            <button
              onClick={handleUpload}
              disabled={!selectedFile || isUploading}
              className="btn btn-primary"
            >
              {isUploading ? 'Uploading...' : 'Upload & Analyze'}
            </button>
            {(selectedFile || analysisId) && (
              <button
                onClick={handleReset}
                disabled={isUploading}
                className="btn btn-secondary"
              >
                Reset
              </button>
            )}
          </div>
        </div>

        {status && (
          <div className="status-section">
            <h3>Analysis Status</h3>
            <div className="status-info">
              <div className="status-badge" data-status={status.status}>
                {formatStatus(status.status)}
              </div>
              {status.progress !== undefined && (
                <div className="progress-bar">
                  <div
                    className="progress-fill"
                    style={{ width: `${status.progress}%` }}
                  />
                  <span className="progress-text">{status.progress}%</span>
                </div>
              )}
              {status.message && (
                <p className="status-message">{status.message}</p>
              )}
              {status.currentStage && (
                <p className="status-stage">Stage: {String(status.currentStage).replace(/([A-Z])/g, ' $1').trim()}</p>
              )}
              
              {/* Progress Preview Buttons */}
              <div className="progress-preview-buttons">
                <button
                  onClick={handleViewPlanImage}
                  disabled={loadingImage || !analysisId}
                  className="btn btn-preview"
                  title="View the converted plan page image"
                >
                  {loadingImage ? 'Loading...' : '📷 View Plan Image'}
                </button>
                {contentTablePage && (
                  <button
                    onClick={handleViewContentTableImage}
                    disabled={loadingImage || !analysisId}
                    className="btn btn-preview"
                    title="View the converted content table page image"
                  >
                    {loadingImage ? 'Loading...' : '📷 View Content Table Image'}
                  </button>
                )}
                <button
                  onClick={handleViewOCRResults}
                  disabled={loadingOCR || !analysisId}
                  className="btn btn-preview"
                  title="View Azure Computer Vision OCR results"
                >
                  {loadingOCR ? 'Loading...' : '🔍 View OCR Results'}
                </button>
              </div>

              {status.status === 'completed' && (
                <div className="results-link">
                  <a href={`#results-${analysisId}`} onClick={(e) => {
                    e.preventDefault()
                    // TODO: Show results when implemented
                    alert('Results view coming soon! Check the browser console for raw results.')
                  }}>
                    View Results →
                  </a>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Image Preview Modal */}
        {showImageModal && imageModalContent && (
          <div className="modal-overlay" onClick={closeImageModal}>
            <div className="modal-content image-modal" onClick={(e) => e.stopPropagation()}>
              <div className="modal-header">
                <h3>{imageModalTitle}</h3>
                <button className="modal-close" onClick={closeImageModal}>×</button>
              </div>
              <div className="modal-body">
                <img src={imageModalContent} alt={imageModalTitle} className="preview-image" />
              </div>
            </div>
          </div>
        )}

        {/* OCR Results Modal */}
        {showOCRModal && ocrData && (
          <div className="modal-overlay" onClick={closeOCRModal}>
            <div className="modal-content ocr-modal" onClick={(e) => e.stopPropagation()}>
              <div className="modal-header">
                <h3>Azure Computer Vision OCR Results</h3>
                <button className="modal-close" onClick={closeOCRModal}>×</button>
              </div>
              <div className="modal-body">
                <div className="ocr-results">
                  <pre className="ocr-json">{JSON.stringify(ocrData, null, 2)}</pre>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

export default DrainagePlanAnalyzer

