import Message from './Message'
import './MessageList.css'

function MessageList({ messages }) {
  return (
    <div className="message-list">
      {messages.map(message => (
        <Message key={message.id} message={message} />
      ))}
    </div>
  )
}

export default MessageList

