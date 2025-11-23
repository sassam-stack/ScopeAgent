import { useState, useRef, useEffect } from 'react'
import axios from 'axios'
import MessageList from './MessageList'
import InputArea from './InputArea'
import AnalysisResult from './AnalysisResult'
import './ChatInterface.css'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'

function ChatInterface() {
  const [messages, setMessages] = useState([
    {
      id: 1,
      type: 'system',
      content: 'Welcome! Please provide an Azure Blob Storage URL to analyze an image schema.',
      timestamp: new Date()
    }
  ])
  const [input, setInput] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [analysisResult, setAnalysisResult] = useState(null)
  const messagesEndRef = useRef(null)

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }

  useEffect(() => {
    scrollToBottom()
  }, [messages, analysisResult])

  const handleSend = async () => {
    if (!input.trim() || isLoading) return

    const userMessage = {
      id: Date.now(),
      type: 'user',
      content: input,
      timestamp: new Date()
    }

    setMessages(prev => [...prev, userMessage])
    setInput('')
    setIsLoading(true)
    setAnalysisResult(null)

    // Check if the message contains a URL
    const urlMatch = input.match(/https?:\/\/[^\s]+/i)
    if (urlMatch) {
      const imageUrl = urlMatch[0]
      
      try {
        const response = await axios.post(`${API_BASE_URL}/analysis/analyze`, {
          imageUrl: imageUrl
        })

        // Computer Vision API returns the result directly (not wrapped in success)
        setAnalysisResult(response.data)
        setMessages(prev => [...prev, {
          id: Date.now() + 1,
          type: 'assistant',
          content: 'Analysis complete! See results below.',
          timestamp: new Date()
        }])
      } catch (error) {
        setMessages(prev => [...prev, {
          id: Date.now() + 1,
          type: 'error',
          content: `Error: ${error.response?.data?.error || error.message || 'Failed to analyze image'}`,
          timestamp: new Date()
        }])
      }
    } else {
      setMessages(prev => [...prev, {
        id: Date.now() + 1,
        type: 'error',
        content: 'Please provide a valid Azure Blob Storage URL (starting with http:// or https://)',
        timestamp: new Date()
      }])
    }

    setIsLoading(false)
  }

  const handleKeyPress = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  return (
    <div className="chat-interface">
      <div className="chat-messages-container">
        <MessageList messages={messages} />
        {isLoading && (
          <div className="loading-indicator">
            <div className="spinner"></div>
            <span>Analyzing image...</span>
          </div>
        )}
        {analysisResult && <AnalysisResult result={analysisResult} />}
        <div ref={messagesEndRef} />
      </div>
      <InputArea
        input={input}
        setInput={setInput}
        onSend={handleSend}
        onKeyPress={handleKeyPress}
        isLoading={isLoading}
      />
    </div>
  )
}

export default ChatInterface

