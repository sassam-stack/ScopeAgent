import './Message.css'

function Message({ message }) {
  const getMessageClass = () => {
    switch (message.type) {
      case 'user':
        return 'message-user'
      case 'assistant':
        return 'message-assistant'
      case 'system':
        return 'message-system'
      case 'error':
        return 'message-error'
      default:
        return 'message-default'
    }
  }

  return (
    <div className={`message ${getMessageClass()}`}>
      <div className="message-content">
        {message.content}
      </div>
      <div className="message-timestamp">
        {new Date(message.timestamp).toLocaleTimeString()}
      </div>
    </div>
  )
}

export default Message

