export default function Pagination({ page, pageSize, totalCount, onPageChange }) {
  if (totalCount <= pageSize) return null

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: '12px',
        marginTop: '20px',
      }}
    >
      <button
        className="btn btn--secondary btn--sm"
        onClick={() => onPageChange(page - 1)}
        disabled={page <= 1}
      >
        Prev
      </button>
      <span>
        Page {page} of {totalPages}
      </span>
      <button
        className="btn btn--secondary btn--sm"
        onClick={() => onPageChange(page + 1)}
        disabled={page * pageSize >= totalCount}
      >
        Next
      </button>
    </div>
  )
}
