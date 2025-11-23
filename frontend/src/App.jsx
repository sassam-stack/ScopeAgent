import DocumentAnalyzer from './components/DocumentAnalyzer'
import './App.css'

function App() {
  return (
    <div className="app">
      <div className="app-container">
        <header className="app-header">
          <h1>Scope Agent</h1>
          <p>Azure Computer Vision Image Analyzer</p>
        </header>
        <DocumentAnalyzer />
      </div>
    </div>
  )
}

export default App

