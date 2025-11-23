import './InputArea.css'

function InputArea({ input, setInput, onSend, onKeyPress, isLoading }) {
  return (
    <div className="input-area">
      <div className="input-container">
        <input
          type="text"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyPress={onKeyPress}
          placeholder="Enter Azure Blob Storage Image URL..."
          disabled={isLoading}
          className="input-field"
        />
        <button
          onClick={onSend}
          disabled={isLoading || !input.trim()}
          className="send-button"
        >
          {isLoading ? 'Analyzing...' : 'Send'}
        </button>
      </div>
      <div className="input-hint">
        Example: https://yourstorageaccount.blob.core.windows.net/container/image.jpg
      </div>
    </div>
  )
}

export default InputArea

