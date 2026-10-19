import { Link } from 'react-router-dom'

export default function Navbar() {
  return (
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
  )
}
