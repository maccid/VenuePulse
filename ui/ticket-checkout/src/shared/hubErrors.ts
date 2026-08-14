export function isAbortedHubStartError(err: unknown): boolean {
  return err instanceof Error && (err.name === 'AbortError' || err.message.includes('stopped during negotiation'))
}
