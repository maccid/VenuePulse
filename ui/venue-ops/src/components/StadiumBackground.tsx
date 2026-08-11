// Fondo decorativo del plano — puramente visual, no son zonas. Separado de StadiumMap.tsx para
// que ese fichero se quede solo con el mapeo zona→forma y el render del SVG.

const FIELD = { x: 88, y: 84, width: 164, height: 112 }
const FIELD_CENTER_X = FIELD.x + FIELD.width / 2
const FIELD_CENTER_Y = FIELD.y + FIELD.height / 2

// Mismo campo que ticket-checkout (para que las curvas de las gradas encajen con el óvalo
// exterior), sin las líneas realistas del campo de fútbol — son decorativas y no aportan a la
// lectura operativa.
export function MatchField() {
  return (
    <>
      <rect x={FIELD.x} y={FIELD.y} width={FIELD.width} height={FIELD.height} rx={4} className="stadium-map__pitch" />
      <line
        x1={FIELD.x}
        y1={FIELD_CENTER_Y}
        x2={FIELD.x + FIELD.width}
        y2={FIELD_CENTER_Y}
        className="stadium-map__midline"
      />
      <circle cx={FIELD_CENTER_X} cy={FIELD_CENTER_Y} r={26} className="stadium-map__midcircle" />
    </>
  )
}

// Escenario de concierto: 2 rectángulos que se solapan formando una "L", con el rótulo en
// vertical dentro del rectángulo grande (ver ADR 0026).
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
