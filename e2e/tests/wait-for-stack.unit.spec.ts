import { test, expect } from '@playwright/test'
import http from 'node:http'
import { waitForStack } from '../wait-for-stack.mjs'

test('resolves once the target starts responding', async () => {
  const server = http.createServer((_req, res) => res.end('ok'))
  await new Promise((resolve, reject) => {
    server.once('error', reject)
    server.listen(0, () => resolve(undefined))
  })
  const port = server.address().port
  setTimeout(() => {}, 0) // server already listening; simulates "ready"
  await expect(waitForStack(`http://localhost:${port}/`, { timeoutMs: 2000, intervalMs: 50 })).resolves.toBeUndefined()
  server.close()
})

test('rejects on timeout when nothing responds', async () => {
  await expect(
    waitForStack('http://localhost:1/', { timeoutMs: 300, intervalMs: 50 }),
  ).rejects.toThrow(/timed out/i)
})
