export class ApiError extends Error {
  readonly status: number
  readonly body: unknown

  constructor(status: number, body: unknown) {
    super(`HTTP ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.body = body
  }

  errorMessage(): string | null {
    const body = this.body as { error?: unknown } | null
    return body && typeof body.error === 'string' ? body.error : null
  }
}
