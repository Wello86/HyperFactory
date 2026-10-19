import { Component } from 'react'

export default class ErrorBoundary extends Component {
  state = { hasError: false }

  static getDerivedStateFromError() {
    return { hasError: true }
  }

  componentDidCatch(error, info) {
    console.error('Unhandled UI error:', error, info)
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="page">
          <div className="error-container">
            <h3>Something went wrong</h3>
            <p>An unexpected error occurred. Please try refreshing the page.</p>
          </div>
        </div>
      )
    }
    return this.props.children
  }
}
