export class PreferenceSaveQueue<Draft, Result> {
  private pending: { draft: Draft; revision: number } | null = null
  private running = false
  private revision = 0
  private send: (draft: Draft) => Promise<Result>
  private saved: (draft: Draft, result: Result, latest: boolean) => void
  private failed: (draft: Draft, error: unknown, latest: boolean) => void

  constructor(
    send: (draft: Draft) => Promise<Result>,
    saved: (draft: Draft, result: Result, latest: boolean) => void,
    failed: (draft: Draft, error: unknown, latest: boolean) => void,
  ) { this.send=send; this.saved=saved; this.failed=failed }

  enqueue(draft: Draft) {
    this.pending = { draft, revision: ++this.revision }
    void this.drain()
  }

  private async drain() {
    if (this.running) return
    this.running = true
    try {
      while (this.pending) {
        const next = this.pending
        this.pending = null
        try {
          const result = await this.send(next.draft)
          this.saved(next.draft, result, next.revision === this.revision)
        } catch (error) {
          this.failed(next.draft, error, next.revision === this.revision)
        }
      }
    } finally {
      this.running = false
    }
  }
}
