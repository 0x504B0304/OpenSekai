#!/usr/bin/env node

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { basename, dirname, extname, resolve } from 'node:path'
import { gunzipSync } from 'node:zlib'

const TICKS_PER_BEAT = 480

const NoteCategory = {
  Normal: 0,
  Long: 1,
  Connection: 2,
  Flick: 3,
  Friction: 4,
  FrictionFlick: 8,
  Guide: 9,
  GuideEnd: 10,
  GuideHidden: 11,
}

const NoteBaseType = {
  Normal: 1,
  Long: 2,
  Flick: 3,
  FrictionFlick: 4,
  Connection: 5,
  Guide: 10,
  Friction: 11,
  GuideEnd: 13,
  GuideHiddenConnection: 14,
}

const supportedNoteArchetype = /^(?:Fake)?(?:(?:Normal|Critical)(?:Head|Tail)?(?:TraceFlick|Trace|Release|Tap|Flick|Tick)|(?:Anchor|Damage))Note$/

const inputArg = process.argv[2]
if (!inputArg) {
  console.error('Usage: node Tools/convert-next-sekai-to-opensekai.mjs <input.bin> [output.json]')
  process.exit(2)
}

const inputPath = resolve(inputArg)
const defaultName = `${basename(inputPath, extname(inputPath))}-open-sekai.json`
const outputPath = resolve(process.argv[3] ?? resolve(dirname(inputPath), defaultName))

const input = readFileSync(inputPath)
const source = input[0] === 0x1f && input[1] === 0x8b ? gunzipSync(input) : input
const level = JSON.parse(source.toString('utf8'))
if (!level || !Array.isArray(level.entities)) throw new Error('Invalid Next Sekai level: entities is missing')

const valueOf = (entity, name, fallback) => {
  const item = entity.data?.find((candidate) => candidate.name === name)
  return item && Object.hasOwn(item, 'value') ? item.value : fallback
}

const refOf = (entity, name) => {
  const item = entity.data?.find((candidate) => candidate.name === name)
  return item && Object.hasOwn(item, 'ref') ? item.ref : undefined
}

const clamp = (value, min, max) => Math.min(max, Math.max(min, value))

const guideLeft = (note) => note.laneStart + (note.GuideStartOffset ?? 0)
const guideRight = (note) => note.laneEnd + 1 + (note.GuideEndOffset ?? 0)

const setGuideBounds = (note, left, right) => {
  note.laneStart = clamp(Math.floor(left), 0, 11)
  note.laneEnd = clamp(Math.ceil(right) - 1, note.laneStart, 11)
  note.GuideStartOffset = left - note.laneStart
  note.GuideEndOffset = right - note.laneEnd - 1
}

const optimizeRectangularGuideRuns = (notes, firstNewId) => {
  const noteById = new Map(notes.map((note) => [note.id, note]))
  const candidates = []
  const candidateIds = new Set()

  for (const start of notes) {
    if (start.category !== NoteCategory.Guide || start.previousConnectionId !== -1 || start.nextConnectionId === -1) continue
    const end = noteById.get(start.nextConnectionId)
    if (!end || end.category !== NoteCategory.GuideEnd || end.nextConnectionId !== -1) continue
    if (Math.abs(guideLeft(start) - guideLeft(end)) > 0.000001
      || Math.abs(guideRight(start) - guideRight(end)) > 0.000001) continue

    candidates.push({
      startTicks: start.ticks,
      endTicks: end.ticks,
      left: guideLeft(start),
      right: guideRight(start),
      start,
      end,
    })
    candidateIds.add(start.id)
    candidateIds.add(end.id)
  }

  const styleGroups = new Map()
  for (const rectangle of candidates) {
    const start = rectangle.start
    const end = rectangle.end
    const styleKey = JSON.stringify([
      start.type,
      start.noteLineType,
      start.speedRatio,
      end.speedRatio,
      start.direction,
      start.isSkip,
      start.ArtGroupId,
    ])
    if (!styleGroups.has(styleKey)) styleGroups.set(styleKey, [])
    styleGroups.get(styleKey).push(rectangle)
  }

  const rebuiltRuns = []
  for (const rectangles of styleGroups.values()) {
    const boundaryTicks = [...new Set(rectangles.flatMap((rectangle) => [rectangle.startTicks, rectangle.endTicks]))]
      .sort((left, right) => left - right)
    let activeRuns = new Map()

    for (let index = 0; index + 1 < boundaryTicks.length; index++) {
      const startTicks = boundaryTicks[index]
      const endTicks = boundaryTicks[index + 1]
      if (endTicks <= startTicks) continue

      const intervals = rectangles
        .filter((rectangle) => rectangle.startTicks <= startTicks && rectangle.endTicks >= endTicks)
        .map((rectangle) => [rectangle.left, rectangle.right])
        .sort((left, right) => left[0] - right[0] || left[1] - right[1])
      const mergedIntervals = []
      for (const interval of intervals) {
        const previous = mergedIntervals[mergedIntervals.length - 1]
        if (previous && interval[0] <= previous[1] + 0.00001) {
          previous[1] = Math.max(previous[1], interval[1])
        } else {
          mergedIntervals.push([...interval])
        }
      }

      const nextActiveRuns = new Map()
      for (const [left, right] of mergedIntervals) {
        const boundsKey = `${left.toFixed(6)}:${right.toFixed(6)}`
        const previous = activeRuns.get(boundsKey)
        if (previous && previous.endTicks === startTicks) {
          previous.endTicks = endTicks
          nextActiveRuns.set(boundsKey, previous)
        } else {
          const run = { startTicks, endTicks, left, right, template: rectangles[0] }
          rebuiltRuns.push(run)
          nextActiveRuns.set(boundsKey, run)
        }
      }
      activeRuns = nextActiveRuns
    }
  }

  let nextId = firstNewId
  const rebuiltNotes = []
  for (const run of rebuiltRuns) {
    const startId = nextId++
    const endId = nextId++
    const start = {
      ...run.template.start,
      id: startId,
      ticks: run.startTicks,
      category: NoteCategory.Guide,
      noteBaseType: NoteBaseType.Guide,
      previousConnectionId: -1,
      nextConnectionId: endId,
    }
    const end = {
      ...run.template.end,
      id: endId,
      ticks: run.endTicks,
      category: NoteCategory.GuideEnd,
      noteBaseType: NoteBaseType.GuideEnd,
      previousConnectionId: startId,
      nextConnectionId: -1,
    }
    setGuideBounds(start, run.left, run.right)
    setGuideBounds(end, run.left, run.right)
    rebuiltNotes.push(start, end)
  }

  const optimized = notes.filter((note) => !candidateIds.has(note.id))
  optimized.push(...rebuiltNotes)
  notes.splice(0, notes.length, ...optimized)
  return {
    rectangularGuideChainsBefore: candidates.length,
    rectangularGuideChainsAfter: rebuiltRuns.length,
  }
}

const sourceNotes = level.entities.filter((entity) => supportedNoteArchetype.test(entity.archetype))
const ignoredNoteArchetypes = [...new Set(
  level.entities
    .filter((entity) => entity.archetype?.endsWith('Note') && !supportedNoteArchetype.test(entity.archetype))
    .map((entity) => entity.archetype),
)]

const idByName = new Map()
const eventCount = level.entities.filter((entity) => entity.archetype === '#BPM_CHANGE').length + 3
sourceNotes.forEach((entity, index) => {
  if (!entity.name) return
  if (idByName.has(entity.name)) throw new Error(`Duplicate note name: ${entity.name}`)
  idByName.set(entity.name, eventCount + index + 1)
})

const previousNameByName = new Map()
for (const entity of sourceNotes) {
  const nextName = refOf(entity, 'next')
  if (nextName === undefined) continue
  if (!entity.name) throw new Error(`Connected ${entity.archetype} is missing a name`)
  if (!idByName.has(nextName)) throw new Error(`Broken next reference: ${entity.name} -> ${nextName}`)
  if (previousNameByName.has(nextName)) throw new Error(`Note ${nextName} has multiple predecessors`)
  previousNameByName.set(nextName, entity.name)
}

let approximatedEaseCount = 0
let clampedGuideCount = 0
let roundedLaneCount = 0
let adjustedGuideTickCount = 0

const toLineType = (ease) => {
  if (ease === 2) return 2 // EaseIn
  if (ease === 3) return 1 // EaseOut
  if (ease === 4 || ease === 5) approximatedEaseCount++
  return 0 // Linear, including unsupported inOut/outIn/none
}

const toDirection = (direction) => {
  if (direction === 1 || direction === 4) return 1
  if (direction === 2 || direction === 5) return 2
  return 0
}

const classify = (archetype, hasPrevious, hasNext) => {
  if (archetype === 'FakeAnchorNote') {
    if (!hasPrevious && hasNext) return [NoteCategory.Guide, NoteBaseType.Guide]
    if (hasPrevious && !hasNext) return [NoteCategory.GuideEnd, NoteBaseType.GuideEnd]
    if (hasPrevious && hasNext) return [NoteCategory.GuideHidden, NoteBaseType.GuideHiddenConnection]
    return [NoteCategory.Friction, NoteBaseType.Friction]
  }

  const isFlick = archetype.includes('Flick')
  const isTrace = archetype.includes('Trace')

  if (!hasPrevious && hasNext) return [NoteCategory.Long, NoteBaseType.Long]
  if (hasPrevious && hasNext) return [NoteCategory.Connection, NoteBaseType.Connection]
  if (hasPrevious) {
    if (isTrace && isFlick) return [NoteCategory.FrictionFlick, NoteBaseType.FrictionFlick]
    if (isFlick) return [NoteCategory.Flick, NoteBaseType.Flick]
    if (isTrace) return [NoteCategory.Friction, NoteBaseType.Friction]
    return [NoteCategory.Long, NoteBaseType.Normal]
  }

  if (isTrace && isFlick) return [NoteCategory.FrictionFlick, NoteBaseType.FrictionFlick]
  if (isFlick) return [NoteCategory.Flick, NoteBaseType.Flick]
  if (isTrace) return [NoteCategory.Friction, NoteBaseType.Friction]
  return [NoteCategory.Normal, NoteBaseType.Normal]
}

const notes = sourceNotes.map((entity, index) => {
  const id = eventCount + index + 1
  const previousName = entity.name ? previousNameByName.get(entity.name) : undefined
  const nextName = refOf(entity, 'next')
  const previousConnectionId = previousName === undefined ? -1 : idByName.get(previousName)
  const nextConnectionId = nextName === undefined ? -1 : idByName.get(nextName)
  const hasPrevious = previousConnectionId !== -1
  const hasNext = nextConnectionId !== -1
  const [category, noteBaseType] = classify(entity.archetype, hasPrevious, hasNext)

  const beat = Number(valueOf(entity, '#BEAT', Number.NaN))
  const lane = Number(valueOf(entity, 'lane', Number.NaN))
  const size = Number(valueOf(entity, 'size', Number.NaN))
  if (!Number.isFinite(beat) || beat < 0 || !Number.isFinite(lane) || !Number.isFinite(size) || size < 0) {
    throw new Error(`Invalid note geometry on ${entity.name ?? entity.archetype}`)
  }

  const rawLeft = lane - size + 6
  const rawRight = lane + size + 6
  let laneStart
  let laneEnd
  let guideStartOffset = 0
  let guideEndOffset = 0
  if (entity.archetype === 'FakeAnchorNote' && (hasPrevious || hasNext)) {
    const left = clamp(rawLeft, 0, 11.99)
    const right = clamp(rawRight, left + 0.001, 12)
    if (left !== rawLeft || right !== rawRight) clampedGuideCount++
    laneStart = clamp(Math.floor(left), 0, 11)
    laneEnd = clamp(Math.ceil(right) - 1, laneStart, 11)
    guideStartOffset = left - laneStart
    guideEndOffset = right - laneEnd - 1
  } else {
    laneStart = clamp(Math.floor(rawLeft + 0.000001), 0, 11)
    laneEnd = clamp(Math.ceil(rawRight - 0.000001) - 1, laneStart, 11)
    if (Math.abs(rawLeft - Math.round(rawLeft)) > 0.000001 || Math.abs(rawRight - Math.round(rawRight)) > 0.000001) {
      roundedLaneCount++
    }
  }

  const segmentKind = Number(valueOf(entity, 'segmentKind', 1))
  const critical = entity.archetype.includes('Critical') || segmentKind === 2 || segmentKind === 52 || segmentKind === 105

  return {
    id,
    ticks: Math.round(beat * TICKS_PER_BEAT),
    laneStart,
    laneEnd,
    category,
    type: critical ? 1 : 0,
    speedRatio: 1,
    noteLineType: toLineType(Number(valueOf(entity, 'connectorEase', 1))),
    noteBaseType,
    previousConnectionId,
    nextConnectionId,
    direction: toDirection(Number(valueOf(entity, 'direction', 0))),
    isSkip: false,
    ArtGroupId: null,
    GuideStartOffset: guideStartOffset,
    GuideEndOffset: guideEndOffset,
  }
})

const bpmEvents = level.entities
  .filter((entity) => entity.archetype === '#BPM_CHANGE')
  .map((entity) => ({
    ticks: Math.round(Number(valueOf(entity, '#BEAT', 0)) * TICKS_PER_BEAT),
    bpm: Number(valueOf(entity, '#BPM', 120)),
  }))
  .sort((left, right) => left.ticks - right.ticks)

if (!bpmEvents.length || bpmEvents.some(({ ticks, bpm }) => ticks < 0 || !Number.isFinite(bpm) || bpm <= 0)) {
  throw new Error('Invalid Next Sekai BPM data')
}

const events = bpmEvents.map(({ ticks, bpm }, index) => ({
  id: index + 1,
  eventType: 0,
  ticks,
  changeValue: bpm,
}))
events.push(
  { id: events.length + 1, eventType: 3, ticks: 0, changeValue: '4/4' },
  { id: events.length + 2, eventType: 1, ticks: 0, changeValue: 1 },
  { id: events.length + 3, eventType: 2, ticks: 0, changeValue: 1 },
)

let noteById = new Map(notes.map((note) => [note.id, note]))
const guideCategories = new Set([NoteCategory.Guide, NoteCategory.GuideEnd, NoteCategory.GuideHidden])
const visitedConnectedNotes = new Set()
for (const root of notes.filter((note) => note.previousConnectionId === -1 && note.nextConnectionId !== -1)) {
  let current = root
  while (current.nextConnectionId !== -1) {
    if (visitedConnectedNotes.has(current.id)) throw new Error(`Cycle detected at note ${current.id}`)
    visitedConnectedNotes.add(current.id)
    const next = noteById.get(current.nextConnectionId)
    if (!next) throw new Error(`Missing next note ${current.nextConnectionId}`)
    if (next.ticks <= current.ticks) {
      if (!guideCategories.has(current.category) || !guideCategories.has(next.category)) {
        throw new Error(`Non-increasing ticks in gameplay chain ${current.id} -> ${next.id}`)
      }
      next.ticks = current.ticks + 1
      adjustedGuideTickCount++
    }
    current = next
  }
  visitedConnectedNotes.add(current.id)
}

const guideOptimization = optimizeRectangularGuideRuns(
  notes,
  Math.max(...events.map((event) => event.id), ...notes.map((note) => note.id)) + 1,
)
noteById = new Map(notes.map((note) => [note.id, note]))

for (const note of notes) {
  if (note.previousConnectionId !== -1) {
    const previous = noteById.get(note.previousConnectionId)
    if (!previous || previous.nextConnectionId !== note.id) throw new Error(`Invalid previous link on note ${note.id}`)
    if (previous.ticks >= note.ticks) throw new Error(`Non-increasing ticks in chain ${previous.id} -> ${note.id}`)
  }
  if (note.nextConnectionId !== -1) {
    const next = noteById.get(note.nextConnectionId)
    if (!next || next.previousConnectionId !== note.id) throw new Error(`Invalid next link on note ${note.id}`)
  }
  if (note.laneStart < 0 || note.laneEnd > 11 || note.laneStart > note.laneEnd) {
    throw new Error(`Invalid OpenSekai lane range on note ${note.id}`)
  }
}

const score = {
  VersionCode: 2,
  MusicScoreEventDataList: events,
  EventArray: [],
  NoteList: notes.sort((left, right) => left.ticks - right.ticks || left.id - right.id),
  ArtGroups: [],
  MusicScoreTicksMax: notes.reduce((max, note) => Math.max(max, note.ticks), 0),
  MusicId: -1,
  FullComboDataHash: null,
}

mkdirSync(dirname(outputPath), { recursive: true })
writeFileSync(outputPath, `${JSON.stringify(score, null, 2)}\n`, 'utf8')

const countByCategory = Object.fromEntries(
  Object.entries(NoteCategory).map(([name, category]) => [name, notes.filter((note) => note.category === category).length]),
)
console.log(JSON.stringify({
  input: inputPath,
  output: outputPath,
  sourceEntities: level.entities.length,
  notes: notes.length,
  events: events.length,
  maxTicks: score.MusicScoreTicksMax,
  categories: countByCategory,
  approximatedEaseCount,
  adjustedGuideTickCount,
  clampedGuideCount,
  roundedLaneCount,
  guideOptimization,
  ignoredNoteArchetypes,
  bgmOffset: level.bgmOffset ?? 0,
}, null, 2))
