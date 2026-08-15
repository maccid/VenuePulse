interface Props {
  message: string | null | undefined
}

export default function InlineError({ message }: Props) {
  if (!message) return null
  return (
    <p className="mt-4 flex items-center gap-2 text-sm text-danger before:content-['✖'] before:text-danger">
      {message}
    </p>
  )
}
