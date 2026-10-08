import assert from 'node:assert/strict'
import test from 'node:test'
import { PreferenceSaveQueue } from '../web-ui/src/lib/preference-save-queue.ts'

const tick = () => new Promise(resolve => setImmediate(resolve))

test('serial saving keeps the latest draft and does not acknowledge intermediate drafts as latest', async () => {
  const calls=[], results=[]
  let finish
  const queue=new PreferenceSaveQueue(value=>{calls.push(value);return new Promise(resolve=>{finish=resolve})},(value,_result,latest)=>results.push([value,latest]),()=>assert.fail('unexpected failure'))
  queue.enqueue(1024)
  queue.enqueue(204)
  queue.enqueue(2048)
  assert.deepEqual(calls,[1024])
  finish('first');await tick()
  assert.deepEqual(calls,[1024,2048])
  assert.deepEqual(results,[[1024,false]])
  finish('last');await tick()
  assert.deepEqual(results,[[1024,false],[2048,true]])
})

test('a failed save does not report success and a later edit can retry', async () => {
  const saved=[],errors=[]
  let fail=true
  const queue=new PreferenceSaveQueue(async value=>{if(fail)throw Error('disk unavailable');return value},value=>saved.push(value),(_value,error,latest)=>errors.push([error.message,latest]))
  queue.enqueue(2048);await tick()
  assert.deepEqual(saved,[])
  assert.deepEqual(errors,[['disk unavailable',true]])
  fail=false;queue.enqueue(4096);await tick()
  assert.deepEqual(saved,[4096])
})

test('pending drafts finish saving even when the caller stops observing the panel', async () => {
  const disk=[]
  let finish
  const queue=new PreferenceSaveQueue(value=>new Promise(resolve=>{finish=()=>{disk.push(value);resolve(value)}}),()=>{},()=>assert.fail('unexpected failure'))
  queue.enqueue(1024);queue.enqueue(2048)
  finish();await tick();finish();await tick()
  assert.deepEqual(disk,[1024,2048])
})
