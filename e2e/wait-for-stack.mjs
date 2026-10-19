export async function waitForStack(url, { timeoutMs = 60000, intervalMs = 1000 } = {}) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    try {
      const res = await fetch(url, { signal: AbortSignal.timeout(intervalMs) })
      if (res.status >= 200 && res.status < 400) return
    } catch {
      // not ready yet — keep polling
    }
    await new Promise((r) => setTimeout(r, intervalMs))
  }
  throw new Error(`waitForStack: timed out waiting for ${url} after ${timeoutMs}ms`)
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const [url, timeoutMs, intervalMs] = process.argv.slice(2)
  if (!url) {
    console.error('usage: wait-for-stack <url> [timeoutMs] [intervalMs]')
    process.exit(1)
  }
  waitForStack(url, {
    timeoutMs: timeoutMs ? Number(timeoutMs) : undefined,
    intervalMs: intervalMs ? Number(intervalMs) : undefined,
  })
    .then(() => process.exit(0))
    .catch((err) => {
      console.error(err.message)
      process.exit(1)
    })
}
