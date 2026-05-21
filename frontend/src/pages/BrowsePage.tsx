import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { searchBrowse } from '../lib/api/browse'
import type { BrowseModDto } from '../types/browse'

export default function BrowsePage() {
  const [input, setInput] = useState('')
  const [query, setQuery] = useState('')
  const [page, setPage] = useState(1)

  const { data, isLoading, isError, isFetching } = useQuery({
    queryKey: ['browse', query, page],
    queryFn: () => searchBrowse(query, page),
    enabled: query.trim().length > 0,
    staleTime: 60_000,
  })

  function handleSearch() {
    const trimmed = input.trim()
    if (!trimmed) return
    setPage(1)
    setQuery(trimmed)
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter') handleSearch()
  }

  const browseApiDown =
    !isLoading &&
    !isError &&
    data !== undefined &&
    data.totalResults === 0 &&
    data.results.length === 0 &&
    query.trim().length > 0 &&
    !isFetching

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-zinc-100">Browse Mods</h1>
      </div>

      {/* Search bar */}
      <div className="flex gap-2">
        <input
          className="input flex-1"
          placeholder="Search lcpdfr.com…"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
        />
        <button
          className="btn-primary"
          onClick={handleSearch}
          disabled={isLoading || isFetching}
        >
          {isFetching ? 'Searching…' : 'Search'}
        </button>
      </div>

      {/* Browse API not running */}
      {browseApiDown && (
        <div className="rounded-lg border border-yellow-900/40 bg-yellow-900/10 px-4 py-3 text-sm text-yellow-400">
          The Browse API is not running. Start it in Settings.
        </div>
      )}

      {/* Loading */}
      {(isLoading || isFetching) && query && (
        <div className="text-zinc-400 text-sm">Searching…</div>
      )}

      {/* Error */}
      {isError && (
        <div className="text-red-400 text-sm">Failed to search. Check your connection.</div>
      )}

      {/* No results */}
      {!isLoading && !isFetching && !isError && data && data.results.length === 0 && data.totalResults > 0 && (
        <div className="text-zinc-500 text-sm">No results found for "{query}".</div>
      )}

      {/* Results grid */}
      {data && data.results.length > 0 && (
        <>
          <div className="text-xs text-zinc-500">
            {data.totalResults} result{data.totalResults !== 1 ? 's' : ''} — page {data.page}
          </div>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {data.results.map((mod) => (
              <ModCard key={mod.id} mod={mod} />
            ))}
          </div>

          {/* Pagination */}
          <div className="flex items-center gap-3">
            <button
              className="btn-secondary"
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
            >
              Previous
            </button>
            <span className="text-sm text-zinc-400">Page {page}</span>
            <button
              className="btn-secondary"
              disabled={!data.hasMore}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </button>
          </div>
        </>
      )}

      {/* Empty state before first search */}
      {!query && (
        <div className="text-zinc-600 text-sm">Enter a search term to find mods on lcpdfr.com.</div>
      )}
    </div>
  )
}

function ModCard({ mod }: { mod: BrowseModDto }) {
  return (
    <div className="rounded-lg border border-zinc-700 bg-zinc-900 flex flex-col overflow-hidden">
      {mod.imageUrl && (
        <img
          src={mod.imageUrl}
          alt={mod.name}
          className="w-full h-32 object-cover bg-zinc-800"
          onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = 'none' }}
        />
      )}
      <div className="p-3 flex flex-col gap-1.5 flex-1">
        <div className="flex items-start justify-between gap-2">
          <span className="text-sm font-medium text-zinc-100 leading-snug">{mod.name}</span>
          {mod.version && (
            <span className="text-xs text-zinc-500 shrink-0 font-mono">{mod.version}</span>
          )}
        </div>
        {mod.author && (
          <span className="text-xs text-zinc-500">by {mod.author}</span>
        )}
        {mod.description && (
          <p className="text-xs text-zinc-400 line-clamp-2">{mod.description}</p>
        )}
        <div className="flex gap-2 mt-auto pt-2">
          {mod.downloadUrl && (
            <button
              className="btn-primary text-xs px-3 py-1"
              onClick={() => window.open(mod.downloadUrl!, '_blank')}
            >
              Download
            </button>
          )}
          {mod.pageUrl && (
            <button
              className="btn-secondary text-xs px-3 py-1"
              onClick={() => window.open(mod.pageUrl!, '_blank')}
            >
              View Page
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
