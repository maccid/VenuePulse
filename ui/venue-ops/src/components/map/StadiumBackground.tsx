const FIELD = { x: 88, y: 84, width: 164, height: 112 }
const FIELD_CENTER_X = FIELD.x + FIELD.width / 2
const FIELD_CENTER_Y = FIELD.y + FIELD.height / 2

export function MatchField() {
  return (
    <g className="stadium-map__field">
      <rect x={FIELD.x} y={FIELD.y} width={FIELD.width} height={FIELD.height} rx={4} className="stadium-map__field-fill" />
      <line
        x1={FIELD_CENTER_X}
        y1={FIELD.y}
        x2={FIELD_CENTER_X}
        y2={FIELD.y + FIELD.height}
        className="stadium-map__field-line"
      />
      <circle cx={FIELD_CENTER_X} cy={FIELD_CENTER_Y} r={26} className="stadium-map__field-line" />
      <circle cx={FIELD_CENTER_X} cy={FIELD_CENTER_Y} r={2} className="stadium-map__field-dot" />
      <rect x={FIELD.x} y={FIELD_CENTER_Y - 30} width={30} height={60} className="stadium-map__field-line" />
      <rect x={FIELD.x} y={FIELD_CENTER_Y - 16} width={14} height={32} className="stadium-map__field-line" />
      <circle cx={FIELD.x + 22} cy={FIELD_CENTER_Y} r={2} className="stadium-map__field-dot" />
      <rect x={FIELD.x + FIELD.width - 30} y={FIELD_CENTER_Y - 30} width={30} height={60} className="stadium-map__field-line" />
      <rect x={FIELD.x + FIELD.width - 14} y={FIELD_CENTER_Y - 16} width={14} height={32} className="stadium-map__field-line" />
      <circle cx={FIELD.x + FIELD.width - 22} cy={FIELD_CENTER_Y} r={2} className="stadium-map__field-dot" />
    </g>
  )
}

export function ConcertStage() {
  return (
    <>
      <rect x="255" y="84" width="43" height="111" className="stadium-map__stage" />
      <rect x="174" y="130" width="82" height="20" className="stadium-map__stage" />
      <text
        x="276.5"
        y="139.5"
        transform="rotate(-90 276.5 139.5)"
        textAnchor="middle"
        dominantBaseline="middle"
        className="stadium-map__stage-label"
      >
        ESCENARIO
      </text>
    </>
  )
}
