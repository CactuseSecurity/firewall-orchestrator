ALTER TABLE request.state_matrix_phase
    ADD COLUMN IF NOT EXISTS phase_visibility_mode varchar NOT NULL DEFAULT 'AnyTask';
