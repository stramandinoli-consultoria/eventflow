CREATE TABLE public.ef_orders (
  id text PRIMARY KEY,
  event_id text NOT NULL,
  stand_id text NOT NULL,
  stand_name text NOT NULL,
  customer_name text NOT NULL,
  customer_phone text,
  items jsonb NOT NULL DEFAULT '[]'::jsonb,
  total numeric NOT NULL DEFAULT 0,
  status text NOT NULL DEFAULT 'awaiting_payment',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);
GRANT SELECT, INSERT, UPDATE, DELETE ON public.ef_orders TO authenticated;
GRANT SELECT, INSERT, UPDATE ON public.ef_orders TO anon;
GRANT ALL ON public.ef_orders TO service_role;
ALTER TABLE public.ef_orders ENABLE ROW LEVEL SECURITY;
CREATE POLICY "ef_orders readable" ON public.ef_orders FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "ef_orders insertable" ON public.ef_orders FOR INSERT TO anon, authenticated WITH CHECK (true);
CREATE POLICY "ef_orders updatable" ON public.ef_orders FOR UPDATE TO anon, authenticated USING (true) WITH CHECK (true);

CREATE TABLE public.ef_order_timeline (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  order_id text NOT NULL REFERENCES public.ef_orders(id) ON DELETE CASCADE,
  status text NOT NULL,
  note text,
  at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ef_order_timeline_order_idx ON public.ef_order_timeline(order_id, at);
GRANT SELECT, INSERT, UPDATE, DELETE ON public.ef_order_timeline TO authenticated;
GRANT SELECT, INSERT ON public.ef_order_timeline TO anon;
GRANT ALL ON public.ef_order_timeline TO service_role;
ALTER TABLE public.ef_order_timeline ENABLE ROW LEVEL SECURITY;
CREATE POLICY "ef_timeline readable" ON public.ef_order_timeline FOR SELECT TO anon, authenticated USING (true);
CREATE POLICY "ef_timeline insertable" ON public.ef_order_timeline FOR INSERT TO anon, authenticated WITH CHECK (true);

CREATE TABLE public.ef_webhooks (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  owner_email text NOT NULL DEFAULT lower(auth.jwt() ->> 'email'),
  url text NOT NULL,
  event_id text,
  stand_name text,
  active boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now(),
  last_status text,
  last_sent_at timestamptz
);
GRANT SELECT, INSERT, UPDATE, DELETE ON public.ef_webhooks TO authenticated;
GRANT ALL ON public.ef_webhooks TO service_role;
ALTER TABLE public.ef_webhooks ENABLE ROW LEVEL SECURITY;
CREATE POLICY "ef_webhooks owner" ON public.ef_webhooks FOR ALL TO authenticated
  USING (owner_email = lower(auth.jwt() ->> 'email'))
  WITH CHECK (owner_email = lower(auth.jwt() ->> 'email'));

ALTER TABLE public.ef_orders REPLICA IDENTITY FULL;
ALTER TABLE public.ef_order_timeline REPLICA IDENTITY FULL;
ALTER PUBLICATION supabase_realtime ADD TABLE public.ef_orders;
ALTER PUBLICATION supabase_realtime ADD TABLE public.ef_order_timeline;