import { useState, useRef, useEffect } from 'react'
import axios from 'axios'
import OuterportResultsViewer from './OuterportResultsViewer'
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
  const [detectedSymbols, setDetectedSymbols] = useState([])
  const [loadingSymbols, setLoadingSymbols] = useState(false)
  const [symbolValidations, setSymbolValidations] = useState({})
  const [validating, setValidating] = useState(false)
  const [symbolsPage, setSymbolsPage] = useState(1)
  const symbolsPerPage = 50
  const [hasPlanImage, setHasPlanImage] = useState(false)
  const [hasOCRResults, setHasOCRResults] = useState(false)
  const [analysisResults, setAnalysisResults] = useState(null)
  const [loadingResults, setLoadingResults] = useState(false)
  const [showResults, setShowResults] = useState(false)
  const [detectedPipes, setDetectedPipes] = useState([])
  const [loadingPipes, setLoadingPipes] = useState(false)
  const [moduleCrops, setModuleCrops] = useState([])
  const [loadingModuleCrops, setLoadingModuleCrops] = useState(false)
  const [verifyingModules, setVerifyingModules] = useState(false)
  const [useOuterport, setUseOuterport] = useState(false)
  const [outerportResults, setOuterportResults] = useState(null)
  const [loadingOuterportResults, setLoadingOuterportResults] = useState(false)
  const [showOuterportModal, setShowOuterportModal] = useState(false)
  const fileInputRef = useRef(null)

  // Poll for status updates and check for available data
  useEffect(() => {
    if (!analysisId) return

    const pollStatus = async () => {
      try {
        const response = await axios.get(`${API_BASE_URL}/drainage/${analysisId}/status`)
        setStatus(response.data)
        
        // Check if plan image is available (based on stage)
        const currentStage = String(response.data.currentStage || '').toLowerCase()
        const statusValue = String(response.data.status || '').toLowerCase()
        
        // Image should be available after OCR extraction stage or later
        const imageAvailable = currentStage.includes('ocr') || 
                              currentStage.includes('symbol') || 
                              currentStage.includes('analyzing') ||
                              currentStage.includes('completed') ||
                              statusValue === 'completed' || statusValue === '2'
        setHasPlanImage(imageAvailable)
        
        // OCR should be available after OCR extraction stage or later
        const ocrAvailable = currentStage.includes('ocr') || 
                             currentStage.includes('symbol') || 
                             currentStage.includes('analyzing') ||
                             currentStage.includes('completed') ||
                             statusValue === 'completed' || statusValue === '2'
        setHasOCRResults(ocrAvailable)
        
        // Stop polling if completed or error (handle both string and enum values)
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
      formData.append('useOuterport', useOuterport.toString())
      
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
    setDetectedSymbols([])
    setSymbolValidations({})
    setModuleCrops([])
    setUseOuterport(false)
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

  // Fetch detected symbols when status is ReadyForValidation
  useEffect(() => {
    if (status && analysisId) {
      const statusValue = String(status.status || '').toLowerCase()
      if ((statusValue === 'ready_for_validation' || statusValue === 'readyforvalidation' || statusValue === '1') && detectedSymbols.length === 0) {
        handleLoadSymbols()
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [status, analysisId])

  // Fetch module crops when status is AwaitingModuleVerification
  useEffect(() => {
    if (status && analysisId) {
      const currentStage = String(status.currentStage || '').toLowerCase()
      if (currentStage.includes('awaitingmoduleverification') || currentStage.includes('awaiting_module_verification')) {
        if (moduleCrops.length === 0) {
          handleLoadModuleCrops()
        }
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [status, analysisId])

  const handleLoadSymbols = async () => {
    if (!analysisId || loadingSymbols) return
    
    setLoadingSymbols(true)
    try {
      const response = await axios.get(`${API_BASE_URL}/drainage/${analysisId}/symbols`)
      // Ensure response.data is treated as an array
      const symbols = Array.isArray(response.data) ? response.data : (response.data || [])
      setDetectedSymbols(symbols)
      
      // Initialize validations (null = not validated yet)
      const initialValidations = {}
      if (Array.isArray(symbols)) {
        symbols.forEach(symbol => {
          initialValidations[symbol.id] = symbol.isModule ?? null
        })
      }
      setSymbolValidations(initialValidations)
    } catch (err) {
      console.error('Error loading symbols:', err)
      if (err.response?.status !== 404) {
        alert(`Error loading symbols: ${err.response?.data?.error || err.message}`)
      }
    } finally {
      setLoadingSymbols(false)
    }
  }

  const handleSymbolValidationChange = (symbolId, isModule) => {
    setSymbolValidations(prev => ({
      ...prev,
      [symbolId]: isModule
    }))
  }

  const handleSubmitValidation = async () => {
    if (!analysisId || validating) return

    // Filter out symbols that haven't been validated (null values)
    const validations = Object.entries(symbolValidations)
      .filter(([_, value]) => value !== null)
      .map(([symbolId, isModule]) => ({
        symbolId,
        isModule
      }))

    // Allow proceeding even with 0 validations (for cases where no symbols were detected)
    if (validations.length === 0 && detectedSymbols.length > 0) {
      alert('Please validate at least one symbol, or mark symbols as "Not a module" if they are not modules.')
      return
    }
    
    // If no symbols detected at all, send empty validation array to proceed
    if (validations.length === 0 && detectedSymbols.length === 0) {
      // This is fine - we'll proceed with text-only analysis
    }

    setValidating(true)
    try {
      await axios.post(`${API_BASE_URL}/drainage/${analysisId}/validate-symbols`, {
        validations
      })
      
      // Clear symbols and reload status
      setDetectedSymbols([])
      setSymbolValidations({})
      
      // Status will be updated by polling
    } catch (err) {
      alert(`Error submitting validation: ${err.response?.data?.error || err.message}`)
    } finally {
      setValidating(false)
    }
  }

  const handleViewResults = async () => {
    if (!analysisId || loadingResults) return
    
    setLoadingResults(true)
    try {
      const response = await axios.get(`${API_BASE_URL}/drainage/${analysisId}/results`)
      setAnalysisResults(response.data)
      setShowResults(true)
    } catch (err) {
      if (err.response?.status === 404) {
        alert('Analysis results not yet available. Please wait for analysis to complete.')
      } else {
        alert(`Error loading results: ${err.response?.data?.error || err.message}`)
      }
    } finally {
      setLoadingResults(false)
    }
  }

  const handleViewPipes = async () => {
    if (!analysisId || loadingPipes) return
    
    setLoadingPipes(true)
    try {
      const response = await axios.get(`${API_BASE_URL}/drainage/${analysisId}/pipes`)
      setDetectedPipes(response.data || [])
      setShowResults(true)
    } catch (err) {
      if (err.response?.status === 404) {
        alert('Pipes not yet available. Please wait for analysis to complete.')
      } else {
        alert(`Error loading pipes: ${err.response?.data?.error || err.message}`)
      }
    } finally {
      setLoadingPipes(false)
    }
  }

  const handleLoadModuleCrops = async () => {
    if (!analysisId || loadingModuleCrops) return
    
    setLoadingModuleCrops(true)
    try {
      const response = await axios.get(`${API_BASE_URL}/drainage/${analysisId}/modules/crops`)
      setModuleCrops(response.data.modules || [])
    } catch (err) {
      console.error('Error loading module crops:', err)
      if (err.response?.status !== 404) {
        alert(`Error loading module crops: ${err.response?.data?.error || err.message}`)
      }
    } finally {
      setLoadingModuleCrops(false)
    }
  }

  const handleVerifyModulesAndContinue = async () => {
    if (!analysisId || verifyingModules) return

    setVerifyingModules(true)
    try {
      await axios.post(`${API_BASE_URL}/drainage/${analysisId}/modules/verify`)
      
      // Clear module crops and reload status
      setModuleCrops([])
      
      // Status will be updated by polling
      alert('Module verification confirmed. Analysis is continuing...')
    } catch (err) {
      alert(`Error verifying modules: ${err.response?.data?.error || err.message}`)
    } finally {
      setVerifyingModules(false)
    }
  }

  const handleViewOuterportResults = async () => {
    if (!analysisId || loadingOuterportResults) return
    
    setLoadingOuterportResults(true)
    try {
      // Fetch Outerport results
      const resultsResponse = await axios.get(`${API_BASE_URL}/drainage/${analysisId}/outerport-results`)
      setOuterportResults(resultsResponse.data)
      
      // Fetch plan image for highlighting
      try {
        const imageResponse = await axios.get(`${API_BASE_URL}/drainage/${analysisId}/image/plan`, {
          responseType: 'blob'
        })
        const imageUrl = URL.createObjectURL(imageResponse.data)
        setImageModalContent(imageUrl)
      } catch (imageErr) {
        console.warn('Could not load plan image for highlighting:', imageErr)
        // Continue without image - the viewer will handle it
      }
      
      setShowOuterportModal(true)
    } catch (err) {
      if (err.response?.status === 404) {
        alert('Outerport results not yet available. Please wait for analysis to complete.')
      } else {
        alert(`Error loading Outerport results: ${err.response?.data?.error || err.message}`)
      }
    } finally {
      setLoadingOuterportResults(false)
    }
  }

  const closeOuterportModal = () => {
    setShowOuterportModal(false)
    setOuterportResults(null)
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

          <div className="form-group">
            <label className="checkbox-label">
              <input
                type="checkbox"
                id="useOuterport"
                checked={useOuterport}
                onChange={(e) => setUseOuterport(e.target.checked)}
                disabled={isUploading}
              />
              <span>Use Outerport for module extraction</span>
            </label>
            <small>If checked, uses Outerport service instead of Azure VR & symbol validation</small>
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
                  className={`btn btn-preview ${hasPlanImage ? 'available' : 'unavailable'}`}
                  title={hasPlanImage ? "View the converted plan page image" : "Plan page image not yet available"}
                >
                  {loadingImage ? 'Loading...' : (
                    <>
                      📷 View Plan Image
                      {hasPlanImage && <span className="status-indicator">✓</span>}
                    </>
                  )}
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
                  className={`btn btn-preview ${hasOCRResults ? 'available' : 'unavailable'}`}
                  title={hasOCRResults ? "View Azure Computer Vision OCR results" : "OCR results not yet available"}
                >
                  {loadingOCR ? 'Loading...' : (
                    <>
                      🔍 View OCR Results
                      {hasOCRResults && <span className="status-indicator">✓</span>}
                    </>
                  )}
                </button>
              </div>

              {/* Symbol Validation Section */}
              {status && (() => {
                const statusStr = String(status.status || '').toLowerCase()
                // Check for ReadyForValidation status (enum value 1 or string variations)
                return statusStr === '1' || 
                       statusStr === 'readyforvalidation' ||
                       statusStr === 'ready_for_validation' ||
                       (statusStr.includes('ready') && statusStr.includes('validation'))
              })() && (
                <div className="symbol-validation-section">
                  <h4>Validate Detected Symbols</h4>
                  {loadingSymbols ? (
                    <p>Loading symbols...</p>
                  ) : detectedSymbols.length === 0 ? (
                    <div className="no-symbols-message">
                      <p>No symbols were detected in the plan page image.</p>
                      <p>This could mean:</p>
                      <ul>
                        <li>The plan doesn't contain the expected symbol patterns (double rectangles, circles with grids, ovals)</li>
                        <li>The image quality may need improvement</li>
                        <li>Symbols may be in a format not yet supported</li>
                      </ul>
                      <p>You can still proceed with text-based analysis by clicking the button below.</p>
                      <button
                        onClick={handleSubmitValidation}
                        className="btn btn-secondary"
                      >
                        Proceed Without Symbols (Text Analysis Only)
                      </button>
                    </div>
                  ) : (
                    <>
                      {detectedSymbols.length > 100 && (
                        <div className="symbols-warning" style={{ 
                          padding: '12px', 
                          backgroundColor: '#fff3cd', 
                          border: '1px solid #ffc107', 
                          borderRadius: '4px', 
                          marginBottom: '16px' 
                        }}>
                          <strong>⚠️ Warning:</strong> {detectedSymbols.length.toLocaleString()} symbols detected. 
                          This is likely due to false positives. Only the first {symbolsPerPage} symbols are shown below. 
                          Consider improving symbol detection filters or proceed with text-based analysis.
                        </div>
                      )}
                      <p className="validation-instruction">
                        Please mark which symbols are modules by checking/unchecking the boxes below.
                        {detectedSymbols.length > symbolsPerPage && (
                          <span> Showing page {symbolsPage} of {Math.ceil(detectedSymbols.length / symbolsPerPage)} ({detectedSymbols.length.toLocaleString()} total symbols)</span>
                        )}
                      </p>
                      {detectedSymbols.length > symbolsPerPage && (
                        <div style={{ marginBottom: '16px', display: 'flex', gap: '8px', alignItems: 'center' }}>
                          <button
                            onClick={() => setSymbolsPage(p => Math.max(1, p - 1))}
                            disabled={symbolsPage === 1}
                            className="btn btn-secondary"
                            style={{ padding: '6px 12px' }}
                          >
                            Previous
                          </button>
                          <span>Page {symbolsPage} of {Math.ceil(detectedSymbols.length / symbolsPerPage)}</span>
                          <button
                            onClick={() => setSymbolsPage(p => Math.min(Math.ceil(detectedSymbols.length / symbolsPerPage), p + 1))}
                            disabled={symbolsPage >= Math.ceil(detectedSymbols.length / symbolsPerPage)}
                            className="btn btn-secondary"
                            style={{ padding: '6px 12px' }}
                          >
                            Next
                          </button>
                        </div>
                      )}
                      <div className="symbols-grid">
                        {detectedSymbols
                          .slice((symbolsPage - 1) * symbolsPerPage, symbolsPage * symbolsPerPage)
                          .map((symbol) => (
                          <div key={symbol.id} className="symbol-card">
                            {symbol.croppedImage ? (
                              <img 
                                src={`data:image/png;base64,${symbol.croppedImage}`}
                                alt={`Symbol ${symbol.id}`}
                                className="symbol-image"
                              />
                            ) : (
                              <div className="symbol-placeholder">No Image</div>
                            )}
                            <div className="symbol-info">
                              <div className="symbol-type">{symbol.type || 'Unknown'}</div>
                              <div className="symbol-confidence">
                                Confidence: {(symbol.confidence * 100).toFixed(0)}%
                              </div>
                            </div>
                            <label className="symbol-checkbox">
                              <input
                                type="checkbox"
                                checked={symbolValidations[symbol.id] === true}
                                onChange={(e) => handleSymbolValidationChange(symbol.id, e.target.checked)}
                              />
                              <span>This is a module</span>
                            </label>
                            {symbolValidations[symbol.id] === false && (
                              <label className="symbol-checkbox">
                                <input
                                  type="checkbox"
                                  checked={true}
                                  onChange={(e) => handleSymbolValidationChange(symbol.id, !e.target.checked)}
                                />
                                <span>Not a module</span>
                              </label>
                            )}
                          </div>
                        ))}
                      </div>
                      <div className="validation-actions">
                        <button
                          onClick={handleSubmitValidation}
                          disabled={validating || Object.values(symbolValidations).filter(v => v !== null).length === 0}
                          className="btn btn-primary"
                        >
                          {validating ? 'Submitting...' : `Submit Validation (${Object.values(symbolValidations).filter(v => v !== null).length} validated)`}
                        </button>
                      </div>
                    </>
                  )}
                </div>
              )}

              {/* Module Verification Section */}
              {status && (() => {
                const currentStage = String(status.currentStage || '').toLowerCase()
                return currentStage.includes('awaitingmoduleverification') || currentStage.includes('awaiting_module_verification')
              })() && (
                <div className="module-verification-section">
                  <h4>Verify Cropped Modules</h4>
                  {loadingModuleCrops ? (
                    <p>Loading module crops...</p>
                  ) : moduleCrops.length === 0 ? (
                    <div className="no-modules-message">
                      <p>No module crops available yet. Please wait...</p>
                    </div>
                  ) : (
                    <>
                      <p className="verification-instruction">
                        Please review the cropped module images below. Verify that all modules are correctly identified before continuing with the analysis.
                      </p>
                      <div className="modules-crops-grid">
                        {moduleCrops.map((module, idx) => (
                          <div key={module.symbolId || idx} className="module-crop-card">
                            <img 
                              src={module.croppedImage}
                              alt={`Module ${module.moduleLabel || idx + 1}`}
                              className="module-crop-image"
                            />
                            <div className="module-crop-info">
                              <div className="module-crop-label">{module.moduleLabel || `Module ${idx + 1}`}</div>
                              <div className="module-crop-confidence">
                                Confidence: {(module.confidence * 100).toFixed(0)}%
                              </div>
                              {module.boundingBox && (
                                <div className="module-crop-bbox">
                                  Position: ({module.boundingBox.x?.toFixed(0) || 0}, {module.boundingBox.y?.toFixed(0) || 0})
                                </div>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                      <div className="verification-actions">
                        <button
                          onClick={handleVerifyModulesAndContinue}
                          disabled={verifyingModules || moduleCrops.length === 0}
                          className="btn btn-primary"
                        >
                          {verifyingModules ? 'Verifying...' : `✓ Verify and Continue (${moduleCrops.length} module${moduleCrops.length !== 1 ? 's' : ''})`}
                        </button>
                      </div>
                    </>
                  )}
                </div>
              )}

              {status && (() => {
                const statusStr = String(status.status || '').toLowerCase()
                return statusStr === 'completed' || statusStr === '2'
              })() && (
                <div className="results-actions">
                  {useOuterport ? (
                    <button
                      onClick={handleViewOuterportResults}
                      disabled={loadingOuterportResults || !analysisId}
                      className="btn btn-primary"
                    >
                      {loadingOuterportResults ? 'Loading...' : '🔍 View Outerport Results'}
                    </button>
                  ) : (
                    <>
                      <button
                        onClick={handleViewResults}
                        disabled={loadingResults || !analysisId}
                        className="btn btn-primary"
                      >
                        {loadingResults ? 'Loading...' : '📊 View Analysis Results'}
                      </button>
                      <button
                        onClick={handleViewPipes}
                        disabled={loadingPipes || !analysisId}
                        className="btn btn-secondary"
                      >
                        {loadingPipes ? 'Loading...' : '🔧 View Detected Pipes'}
                      </button>
                    </>
                  )}
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
                  {/* Summary Statistics */}
                  {ocrData.pages && ocrData.pages.length > 0 && (
                    <div className="ocr-summary">
                      <h4>Summary</h4>
                      <div className="ocr-stats">
                        <div className="stat-item">
                          <span className="stat-label">Pages:</span>
                          <span className="stat-value">{ocrData.pages.length}</span>
                        </div>
                        <div className="stat-item">
                          <span className="stat-label">Total Lines:</span>
                          <span className="stat-value">
                            {ocrData.pages.reduce((sum, page) => sum + (page.lines?.length || 0), 0)}
                          </span>
                        </div>
                        <div className="stat-item">
                          <span className="stat-label">Total Words:</span>
                          <span className="stat-value">
                            {ocrData.pages.reduce((sum, page) => 
                              sum + (page.lines?.reduce((lineSum, line) => lineSum + (line.words?.length || 0), 0) || 0), 0
                            )}
                          </span>
                        </div>
                      </div>
                    </div>
                  )}
                  
                  {/* Text Preview */}
                  {ocrData.pages && ocrData.pages.length > 0 && (
                    <div className="ocr-text-preview">
                      <h4>Extracted Text Preview</h4>
                      <div className="ocr-text-content">
                        {ocrData.pages.map((page, pageIdx) => (
                          <div key={pageIdx} className="ocr-page">
                            <h5>Page {page.pageNumber || pageIdx + 1}</h5>
                            {page.lines && page.lines.map((line, lineIdx) => (
                              <div key={lineIdx} className="ocr-line">
                                {line.text}
                              </div>
                            ))}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                  
                  {/* Full JSON (Collapsible) */}
                  <details className="ocr-json-details">
                    <summary>View Full JSON Data</summary>
                    <pre className="ocr-json">{JSON.stringify(ocrData, null, 2)}</pre>
                  </details>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Outerport Results Modal */}
        {showOuterportModal && outerportResults && (
          <OuterportResultsViewer
            imageUrl={imageModalContent}
            results={outerportResults}
            onClose={closeOuterportModal}
          />
        )}

        {/* Results Modal */}
        {showResults && (analysisResults || detectedPipes.length > 0) && (
          <div className="modal-overlay" onClick={() => setShowResults(false)}>
            <div className="modal-content results-modal" onClick={(e) => e.stopPropagation()}>
              <div className="modal-header">
                <h3>Analysis Results</h3>
                <button className="modal-close" onClick={() => setShowResults(false)}>×</button>
              </div>
              <div className="modal-body">
                {/* Analysis Results Summary */}
                {analysisResults && (
                  <div className="results-summary">
                    <h4>Summary</h4>
                    <div className="results-stats">
                      <div className="stat-item">
                        <span className="stat-label">Modules Found:</span>
                        <span className="stat-value">{analysisResults.modules?.length || 0}</span>
                      </div>
                      <div className="stat-item">
                        <span className="stat-label">Pipes Found:</span>
                        <span className="stat-value">{analysisResults.pipes?.length || 0}</span>
                      </div>
                      <div className="stat-item">
                        <span className="stat-label">Overall Confidence:</span>
                        <span className="stat-value">
                          {analysisResults.confidence?.average 
                            ? `${(analysisResults.confidence.average * 100).toFixed(1)}%`
                            : 'N/A'}
                        </span>
                      </div>
                      {analysisResults.scale && (
                        <div className="stat-item">
                          <span className="stat-label">Scale:</span>
                          <span className="stat-value">{analysisResults.scale.text || 'Not found'}</span>
                        </div>
                      )}
                    </div>
                  </div>
                )}

                {/* Pipes Section */}
                {(detectedPipes.length > 0 || (analysisResults?.pipes?.length > 0)) && (
                  <div className="pipes-section">
                    <h4>🔧 Detected Pipes ({detectedPipes.length > 0 ? detectedPipes.length : analysisResults?.pipes?.length || 0})</h4>
                    <p className="section-description">
                      Pipes are detected by finding collinear "ST" labels from OCR. Each pipe represents a line segment connecting multiple ST labels.
                    </p>
                    {((detectedPipes.length > 0 ? detectedPipes : analysisResults?.pipes || []).length === 0) ? (
                      <div className="no-data-message">
                        No pipes detected. This could mean:
                        <ul>
                          <li>No "ST" labels were found in the OCR results</li>
                          <li>ST labels were found but not grouped into collinear patterns</li>
                          <li>Pipes need at least 2 ST labels to be detected</li>
                        </ul>
                      </div>
                    ) : (
                      <div className="pipes-list">
                        {(detectedPipes.length > 0 ? detectedPipes : analysisResults?.pipes || []).map((pipe, idx) => (
                          <div key={pipe.id || idx} className="pipe-card">
                            <div className="pipe-header">
                              <span className="pipe-id">🔧 Pipe {idx + 1} (ID: {pipe.id?.substring(0, 8) || 'N/A'})</span>
                              <span className="pipe-confidence">
                                Confidence: {(pipe.confidence * 100).toFixed(1)}%
                              </span>
                            </div>
                            <div className="pipe-details">
                              <div className="pipe-info-highlight">
                                <strong>📍 ST Labels Found:</strong> <span className="highlight-value">{pipe.stLabels?.length || 0}</span>
                                {pipe.stLabels && pipe.stLabels.length > 0 && (
                                  <span className="info-note"> (These ST labels form the pipe line)</span>
                                )}
                              </div>
                              {pipe.line && pipe.line.startPoint && pipe.line.endPoint ? (
                                <div className="pipe-line-info">
                                  <div className="pipe-info-item">
                                    <strong>📏 Line Segment:</strong>
                                  </div>
                                  <div className="pipe-info-item indent">
                                    <strong>Start:</strong> ({pipe.line.startPoint.x?.toFixed(1) || 0}, {pipe.line.startPoint.y?.toFixed(1) || 0}) px
                                  </div>
                                  <div className="pipe-info-item indent">
                                    <strong>End:</strong> ({pipe.line.endPoint.x?.toFixed(1) || 0}, {pipe.line.endPoint.y?.toFixed(1) || 0}) px
                                  </div>
                                  <div className="pipe-info-item indent">
                                    <strong>Length:</strong> <span className="highlight-value">
                                      {Math.sqrt(
                                        Math.pow(pipe.line.endPoint.x - pipe.line.startPoint.x, 2) +
                                        Math.pow(pipe.line.endPoint.y - pipe.line.startPoint.y, 2)
                                      ).toFixed(1)} px
                                    </span>
                                  </div>
                                </div>
                              ) : (
                                <div className="pipe-info-item warning">
                                  ⚠️ No line segment data available
                                </div>
                              )}
                              {pipe.specification ? (
                                <div className="pipe-info-item highlight">
                                  <strong>📋 Specification:</strong> {pipe.specification.text || `${pipe.specification.diameter}" ${pipe.specification.material || ''}`}
                                </div>
                              ) : (
                                <div className="pipe-info-item muted">
                                  <strong>📋 Specification:</strong> <span className="muted-text">Not detected</span>
                                </div>
                              )}
                              {pipe.flowDirection ? (
                                <div className="pipe-info-item">
                                  <strong>➡️ Flow Direction:</strong> {pipe.flowDirection.direction || 'Unknown'}
                                </div>
                              ) : (
                                <div className="pipe-info-item muted">
                                  <strong>➡️ Flow Direction:</strong> <span className="muted-text">Not detected</span>
                                </div>
                              )}
                              {pipe.connections && pipe.connections.length > 0 ? (
                                <div className="pipe-info-item highlight">
                                  <strong>🔗 Connections:</strong> {pipe.connections.length} module(s)
                                </div>
                              ) : (
                                <div className="pipe-info-item muted">
                                  <strong>🔗 Connections:</strong> <span className="muted-text">Not yet associated with modules</span>
                                </div>
                              )}
                            </div>
                            {pipe.stLabels && pipe.stLabels.length > 0 && (
                              <details className="pipe-st-labels">
                                <summary>📌 View {pipe.stLabels.length} ST Label{pipe.stLabels.length !== 1 ? 's' : ''} (Click to expand)</summary>
                                <div className="st-labels-list">
                                  {pipe.stLabels.map((stLabel, stIdx) => (
                                    <div key={stIdx} className="st-label-item">
                                      <span className="st-label-number">ST #{stIdx + 1}:</span>
                                      <span className="st-label-position">📍 ({stLabel.position?.x?.toFixed(1) || 0}, {stLabel.position?.y?.toFixed(1) || 0})</span>
                                      <span className="st-label-confidence">{(stLabel.confidence * 100).toFixed(1)}%</span>
                                    </div>
                                  ))}
                                </div>
                              </details>
                            )}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}

                {/* Modules Section */}
                {analysisResults?.modules && analysisResults.modules.length > 0 && (
                  <div className="modules-section">
                    <h4>Detected Modules ({analysisResults.modules.length})</h4>
                    <div className="modules-list">
                      {analysisResults.modules.map((module, idx) => (
                        <div key={idx} className="module-card">
                          <div className="module-header">
                            <span className="module-label">{module.label || `Module ${idx + 1}`}</span>
                            {module.metadata?.confidence && (
                              <span className="module-confidence">
                                Confidence: {(module.metadata.confidence * 100).toFixed(1)}%
                              </span>
                            )}
                          </div>
                          {module.location && (
                            <div className="module-info-item">
                              <strong>Location:</strong> ({module.location.x?.toFixed(1) || 0}, {module.location.y?.toFixed(1) || 0})
                            </div>
                          )}
                          {module.connections && module.connections.length > 0 && (
                            <div className="module-info-item">
                              <strong>Connections:</strong> {module.connections.length} pipe(s)
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Full JSON (Collapsible) */}
                <details className="results-json-details">
                  <summary>View Full JSON Data</summary>
                  <pre className="results-json">
                    {JSON.stringify(analysisResults || { pipes: detectedPipes }, null, 2)}
                  </pre>
                </details>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

export default DrainagePlanAnalyzer

