ALTER TABLE public.ef_webhooks ADD COLUMN IF NOT EXISTS secret text NOT NULL DEFAULT encode(extensions.gen_random_bytes(32), 'hex');

CREATE TABLE public.ef_webhook_deliveries (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  webhook_id uuid NOT NULL REFERENCES public.ef_webhooks(id) ON DELETE CASCADE,
  owner_email text NOT NULL DEFAULT lower(auth.jwt() ->> 'email'),
  order_id text,
  payload jsonb NOT NULL,
  status text NOT NULL DEFAULT 'pending',
  attempts int NOT NULL DEFAULT 0,
  max_attempts int NOT NULL DEFAULT 6,
  next_attempt_at timestamptz NOT NULL DEFAULT now(),
  last_error text,
  last_response_status int,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ef_webhook_deliveries_due_idx ON public.ef_webhook_deliveries (status, next_attempt_at);
GRANT SELECT, INSERT, UPDATE ON public.ef_webhook_deliveries TO authenticated;
GRANT ALL ON public.ef_webhook_deliveries TO service_role;
ALTER TABLE public.ef_webhook_deliveries ENABLE ROW LEVEL SECURITY;
CREATE POLICY "deliveries owner" ON public.ef_webhook_deliveries FOR ALL TO authenticated
  USING (owner_email = lower(auth.jwt() ->> 'email'))
  WITH CHECK (owner_email = lower(auth.jwt() ->> 'email'));

CREATE TABLE public.ef_webhook_attempts (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  delivery_id uuid NOT NULL REFERENCES public.ef_webhook_deliveries(id) ON DELETE CASCADE,
  owner_email text NOT NULL DEFAULT lower(auth.jwt() ->> 'email'),
  attempt int NOT NULL,
  ok boolean NOT NULL,
  response_status int,
  error text,
  duration_ms int,
  at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ef_webhook_attempts_delivery_idx ON public.ef_webhook_attempts (delivery_id);
GRANT SELECT, INSERT ON public.ef_webhook_attempts TO authenticated;
GRANT ALL ON public.ef_webhook_attempts TO service_role;
ALTER TABLE public.ef_webhook_attempts ENABLE ROW LEVEL SECURITY;
CREATE POLICY "attempts owner" ON public.ef_webhook_attempts FOR ALL TO authenticated
  USING (owner_email = lower(auth.jwt() ->> 'email'))
  WITH CHECK (owner_email = lower(auth.jwt() ->> 'email'));