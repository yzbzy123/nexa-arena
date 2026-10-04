import { useId, type ReactNode } from "react"
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import {
  Select, SelectContent, SelectGroup, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select"

export type ActionRunner = <T>(action: string, data?: Record<string, unknown>, success?: string) => Promise<T | null>

export function PageHeading({ title, children }: { title: string; children?: ReactNode }) {
  return <div className="page-heading"><div><h1>{title}</h1>{children && <p>{children}</p>}</div></div>
}

export function Choice({ label, value, options, onChange, disabled }: {
  label: string; value: string; options: { value: string; label: string }[];
  onChange: (value: string) => void; disabled?: boolean
}) {
  const id=useId()
  return <FieldGroup><Field data-disabled={disabled||undefined}><FieldLabel id={id+"-label"} htmlFor={id}>{label}</FieldLabel>
    <Select value={value} onValueChange={onChange} disabled={disabled}>
      <SelectTrigger id={id} aria-labelledby={id+"-label"} className="w-full"><SelectValue placeholder={label} /></SelectTrigger>
      <SelectContent><SelectGroup>{options.map((item) =>
        <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>)}</SelectGroup></SelectContent>
    </Select>
  </Field></FieldGroup>
}

export function NumberField({ label, value, onChange, min = 0, max, step = 1, disabled }: {
  label: string; value: number; onChange: (value: number) => void; min?: number;
  max?: number; step?: number; disabled?: boolean
}) {
  const id=useId()
  return <FieldGroup><Field data-disabled={disabled||undefined}><FieldLabel htmlFor={id}>{label}</FieldLabel>
    <Input id={id} type="number" inputMode="decimal" min={min} max={max} step={step} value={value}
      disabled={disabled} onChange={(event) => onChange(Number(event.target.value))} />
  </Field></FieldGroup>
}
