import { formatDate, getStatusBadgeClass } from '../utils/format'

export default function ConferenceInfoTable({ conference }) {
  return (
    <div className="card">
      <table className="info-table">
        <tbody>
          <tr>
            <td>Location</td>
            <td>{conference?.location || '—'}</td>
          </tr>
          <tr>
            <td>Venue</td>
            <td>{conference?.venue || '—'}</td>
          </tr>
          <tr>
            <td>Start Date</td>
            <td>{formatDate(conference?.startDate)}</td>
          </tr>
          <tr>
            <td>End Date</td>
            <td>{formatDate(conference?.endDate)}</td>
          </tr>
          <tr>
            <td>Status</td>
            <td>
              <span className={getStatusBadgeClass(conference?.status)}>
                {conference?.status || 'Upcoming'}
              </span>
            </td>
          </tr>
          <tr>
            <td>Max Attendees</td>
            <td>{conference?.maxAttendees ?? '—'}</td>
          </tr>
          <tr>
            <td>Organizer</td>
            <td>{conference?.organizerName || '—'}</td>
          </tr>
        </tbody>
      </table>
    </div>
  )
}
