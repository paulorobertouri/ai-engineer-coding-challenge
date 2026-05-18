import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SourceQualityInspector } from './SourceQualityInspector'
import type { SourceQualityReportResponse } from '../types/chat'

describe('SourceQualityInspector', () => {
  it('renders loading, error and empty states', () => {
    const { rerender } = render(
      <SourceQualityInspector report={null} isLoading={true} errorMessage={null} />,
    )
    expect(screen.getByText(/evaluating source quality/i)).toBeInTheDocument()

    rerender(
      <SourceQualityInspector
        report={null}
        isLoading={false}
        errorMessage={'Unable to inspect quality'}
      />,
    )
    expect(screen.getByText(/unable to inspect quality/i)).toBeInTheDocument()

    rerender(
      <SourceQualityInspector report={null} isLoading={false} errorMessage={null} />,
    )
    expect(
      screen.getByText(/select a citation to inspect source quality metrics/i),
    ).toBeInTheDocument()
  })

  it('renders report metrics and outlier labels', () => {
    const report: SourceQualityReportResponse = {
      source: 'SOP.md',
      knowledgeBaseId: 'default',
      totalChunks: 12,
      duplicateSectionCount: 1,
      weakExtractionZoneCount: 2,
      shortestChunks: [
        {
          chunkId: 'short-1',
          sectionTitle: 'Store Opening',
          characterCount: 42,
          startLine: 3,
          endLine: 5,
        },
      ],
      longestChunks: [
        {
          chunkId: 'long-1',
          sectionTitle: '',
          characterCount: 420,
        },
      ],
    }

    render(
      <SourceQualityInspector report={report} isLoading={false} errorMessage={null} />,
    )

    expect(screen.getByText(/SOP.md · chunks 12/i)).toBeInTheDocument()
    expect(screen.getByText(/duplicate sections: 1/i)).toBeInTheDocument()
    expect(screen.getByText(/weak extraction zones: 2/i)).toBeInTheDocument()
    expect(screen.getByText(/short · 42 chars/i)).toBeInTheDocument()
    expect(screen.getByText(/long · 420 chars/i)).toBeInTheDocument()
    expect(screen.getByText(/lines 3-5/i)).toBeInTheDocument()
    expect(screen.getByText(/lines n\/a/i)).toBeInTheDocument()
  })
})
