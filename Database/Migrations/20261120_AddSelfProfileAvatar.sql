BEGIN;

CREATE SCHEMA IF NOT EXISTS media;

CREATE TABLE media.files
(
    id uuid PRIMARY KEY DEFAULT uuid_generate_v7(),
    uploader_user_id uuid NOT NULL
        REFERENCES auth.users(id) ON DELETE RESTRICT,
    category varchar(30) NOT NULL,
    cloudinary_public_id varchar(512) NOT NULL,
    secure_url text NOT NULL,
    file_name varchar(255) NOT NULL,
    content_type varchar(100) NOT NULL,
    size_bytes bigint NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'TEMPORARY',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid NULL
        REFERENCES auth.users(id) ON DELETE RESTRICT,
    deleted_at timestamptz NULL,
    deleted_by uuid NULL
        REFERENCES auth.users(id) ON DELETE RESTRICT,
    CONSTRAINT uq_files_id_uploader_user
        UNIQUE (id, uploader_user_id),
    CONSTRAINT uq_files_cloudinary_public_id
        UNIQUE (cloudinary_public_id),
    CONSTRAINT ck_files_category
        CHECK (category IN ('branch', 'court', 'avatar', 'tournament', 'promotion')),
    CONSTRAINT ck_files_content_type
        CHECK (content_type IN ('image/jpeg', 'image/jpg', 'image/png', 'image/webp')),
    CONSTRAINT ck_files_size_bytes
        CHECK (size_bytes BETWEEN 1 AND 5242880),
    CONSTRAINT ck_files_status
        CHECK (status IN ('TEMPORARY', 'ATTACHED', 'REPLACED'))
);

CREATE INDEX ix_files_uploader_status_created_at
    ON media.files (uploader_user_id, status, created_at)
    WHERE deleted_at IS NULL;

ALTER TABLE auth.users
    ADD COLUMN avatar_file_id uuid NULL,
    ADD CONSTRAINT fk_users_avatar_file_owner
        FOREIGN KEY (avatar_file_id, id)
        REFERENCES media.files (id, uploader_user_id)
        ON DELETE RESTRICT;

CREATE INDEX ix_users_avatar_file_id
    ON auth.users (avatar_file_id)
    WHERE avatar_file_id IS NOT NULL;

COMMIT;
