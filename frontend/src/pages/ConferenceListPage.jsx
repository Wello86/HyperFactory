import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { getConferences } from '../api'
import ConferenceCard from '../components/ConferenceCard'
import Pagination from '../components/Pagination'

const PAGE_SIZE = 20

export default function ConferenceListPage() {
  const [conferences, setConferences] = useState([])
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [search, setSearch] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    getConferences(page, PAGE_SIZE, { search, from: fromDate, to: toDate })
      .then((data) => {
        if (cancelled) return
        setConferences(Array.isArray(data.data) ? data.data : [])
        setTotalCount(data.totalCount || 0)
        setLoading(false)
      })
      .catch((err) => {
        if (cancelled) return
        setError(err.message || 'Failed to load conferences')
        setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [page, search, fromDate, toDate])

  const handleSearchChange = (e) => {
    setSearch(e.target.value)
    setPage(1)
  }
  const handleFromChange = (e) => {
    setFromDate(e.target.value)
    setPage(1)
  }
  const handleToChange = (e) => {
    setToDate(e.target.value)
    setPage(1)
  }

  return (
    <div>
      <nav className="navbar">
        <Link to="/" className="navbar__brand">
          BrownEvents
        </Link>
        <Link to="/" className="navbar__link">
          Conferences
        </Link>
        <Link to="/speakers" className="navbar__link">
          Speakers
        </Link>
      </nav>

      <div className="page">
        <div
          className="page-header"
          style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}
        >
          <h1 className="page-header__title">Conferences</h1>
          <Link to="/conferences/new" className="btn btn--primary">
            Add Conference
          </Link>
        </div>

        <div className="filters" style={{ display: 'flex', gap: '12px', marginBottom: '16px' }}>
          <input
            type="text"
            placeholder="Search by title or description"
            value={search}
            onChange={handleSearchChange}
            aria-label="Search conferences"
          />
          <input type="date" value={fromDate} onChange={handleFromChange} aria-label="From date" />
          <input type="date" value={toDate} onChange={handleToChange} aria-label="To date" />
        </div>

        {loading && (
          <div className="loading-container">
            <div className="spinner" />
            <p>Loading conferences...</p>
          </div>
        )}

        {error && (
          <div className="error-container">
            <h3>Failed to load conferences</h3>
            <p>{error}</p>
          </div>
        )}

        {!loading && !error && conferences.length === 0 && (
          <div style={{ textAlign: 'center', color: '#666', padding: '40px 0' }}>
            No conferences match your filters.
          </div>
        )}

        <div className="card-grid">
          {conferences.map((conf) => (
            <ConferenceCard key={conf.id} conference={conf} />
          ))}
        </div>

        {!loading && !error && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            onPageChange={setPage}
          />
        )}
      </div>
    </div>
  )
}
