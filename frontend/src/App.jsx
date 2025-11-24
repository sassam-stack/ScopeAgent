import DrainagePlanAnalyzer from './components/DrainagePlanAnalyzer'
import './App.css'

function App() {
  return (
    <div className="app">
      <div className="app-container">
        <header className="app-header">
          <h1>Scope Agent</h1>
          <p>Construction Drainage Plan Analyzer</p>
        </header>
        <DrainagePlanAnalyzer />
      </div>
    </div>
  )
}

export default App

