import type { StandShape } from './ZoneShape'

// Curvas base de las gradas, hacia el óvalo exterior (8,8,324,264, rx 85) — Este/Oeste (partido)
// y Grada 3 (concierto) reutilizan la de Oeste girada con `transform`, porque el óvalo no es
// cuadrado y una rotación simple no encajaría con el campo/escenario. Coordenadas ajustadas a
// mano sobre el plano, no calculadas — ver ADR 0024 (partido) y ADR 0026 (concierto).
const ESTE_OESTE_TRANSFORM = 'matrix(-0.00261, -0.70282102, 0.98233294, 0, 27.46486001, -0.33711338)'
const ESTE_OESTE_ORIGIN = '170.725px 140.696px'
const NORTE_PATH =
  'M 87.59 82.282 L 46.307 29.872 C 87.035 7.34 254.481 6.173 293.641 30.59 L 251.641 82.641 L 87.59 82.282'
const SUR_PATH =
  'M 88.602 199.11 L 47.592 249.066 C 81.504 271.326 256.502 272.935 294.653 250.802 L 252.653 198.751 L 88.602 199.11'
const OESTE_PATH =
  'M 249.914 26.556 L 325.097 -15.981 C 308.911 -37.15 49.131 -42.901 18.481 -15.72 L 91.272 26.915 L 249.914 26.556'

export const MATCH_STANDS: Record<string, StandShape> = {
  'Grada VIP Norte': { path: NORTE_PATH, labelX: 170, labelY: 57, labelLines: ['Grada VIP', 'Norte'], lightX: 170, lightY: 36 },
  'Grada VIP Sur': { path: SUR_PATH, labelX: 170, labelY: 222, labelLines: ['Grada VIP', 'Sur'], lightX: 170, lightY: 250 },
  'Grada Este': {
    path: OESTE_PATH,
    transform: ESTE_OESTE_TRANSFORM,
    transformOrigin: ESTE_OESTE_ORIGIN,
    labelX: 286,
    labelY: 134,
    labelLines: ['Grada', 'Este'],
    lightX: 286,
    lightY: 112,
  },
  'Grada Oeste': {
    path: OESTE_PATH,
    transform: ESTE_OESTE_TRANSFORM,
    transformOrigin: ESTE_OESTE_ORIGIN,
    labelX: 54,
    labelY: 134,
    labelLines: ['Grada', 'Oeste'],
    lightX: 54,
    lightY: 112,
  },
}

// Concierto: 3 gradas (mismas curvas que partido) + Pista/VIP como rectángulos rectos (sin
// esquinas redondeadas, a diferencia de las gradas de partido) — ver ADR 0026.
export const CONCERT_STANDS: Record<string, StandShape> = {
  'Grada 1': { path: NORTE_PATH, labelX: 170, labelY: 48, labelLines: ['Grada 1'], lightX: 170, lightY: 30 },
  'Grada 2': { path: SUR_PATH, labelX: 170, labelY: 232, labelLines: ['Grada 2'], lightX: 170, lightY: 250 },
  'Grada 3': {
    path: OESTE_PATH,
    transform: ESTE_OESTE_TRANSFORM,
    transformOrigin: ESTE_OESTE_ORIGIN,
    labelX: 54,
    labelY: 134,
    labelLines: ['Grada 3'],
    lightX: 54,
    lightY: 112,
  },
  Pista: {
    rect: { x: 93, y: 89, width: 73, height: 101 },
    labelX: 129.5,
    labelY: 143,
    labelLines: ['Pista'],
    lightX: 129.5,
    lightY: 122,
  },
  'VIP 1': {
    rect: { x: 175, y: 90, width: 73, height: 37 },
    labelX: 211.5,
    labelY: 117,
    labelLines: ['VIP 1'],
    lightX: 211.5,
    lightY: 100,
  },
  'VIP 2': {
    rect: { x: 175, y: 153, width: 73, height: 37 },
    labelX: 211.5,
    labelY: 164,
    labelLines: ['VIP 2'],
    lightX: 211.5,
    lightY: 181,
  },
}
