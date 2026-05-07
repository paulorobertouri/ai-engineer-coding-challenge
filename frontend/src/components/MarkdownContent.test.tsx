import { render, screen } from '@testing-library/react'
import { MarkdownContent } from './MarkdownContent'
import { describe, it, expect } from 'vitest'

describe('MarkdownContent', () => {
  it('renders plain text', () => {
    render(<MarkdownContent content="Hello world" />)
    expect(screen.getByText('Hello world')).toBeInTheDocument()
  })

  it('renders markdown headings', () => {
    render(<MarkdownContent content="## Section Title" />)
    expect(screen.getByRole('heading', { name: 'Section Title' })).toBeInTheDocument()
  })

  it('renders links with target _blank and noopener noreferrer', () => {
    render(<MarkdownContent content="[Click here](https://example.com)" />)
    const link = screen.getByRole('link', { name: 'Click here' })
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it('applies additional className to the wrapper', () => {
    const { container } = render(<MarkdownContent content="text" className="custom-class" />)
    expect(container.firstChild).toHaveClass('custom-class')
  })

  it('renders bold text', () => {
    render(<MarkdownContent content="**bold text**" />)
    expect(screen.getByText('bold text')).toBeInTheDocument()
  })

  it('renders unordered list items', () => {
    render(<MarkdownContent content={'- item one\n- item two'} />)
    expect(screen.getByText('item one')).toBeInTheDocument()
    expect(screen.getByText('item two')).toBeInTheDocument()
  })

  it('renders a GFM table with custom th and td', () => {
    const tableMarkdown = '| Name | Age |\n| --- | --- |\n| Alice | 30 |'
    render(<MarkdownContent content={tableMarkdown} />)
    expect(screen.getByRole('table')).toBeInTheDocument()
    expect(screen.getByText('Name')).toBeInTheDocument()
    expect(screen.getByText('Alice')).toBeInTheDocument()
  })

  it('renders th elements inside the table', () => {
    const tableMarkdown = '| Col A | Col B |\n| --- | --- |\n| val | val |'
    render(<MarkdownContent content={tableMarkdown} />)
    const headers = screen.getAllByRole('columnheader')
    expect(headers.length).toBeGreaterThanOrEqual(2)
  })

  it('renders td elements inside the table', () => {
    const tableMarkdown = '| Col |\n| --- |\n| cell value |'
    render(<MarkdownContent content={tableMarkdown} />)
    expect(screen.getByText('cell value')).toBeInTheDocument()
  })
})
