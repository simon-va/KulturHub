create table public.users
(
    user_id    uuid                     not null
        primary key
        references auth.users (id)
        on delete cascade,
    first_name text                     not null,
    last_name  text                     not null,
    is_admin   boolean default false    not null,
    is_deleted boolean default false    not null,
    deleted_at timestamp with time zone,
    email      text    default ''::text not null
);

alter table public.users
    owner to postgres;

grant delete, insert, references, select, trigger, truncate, update on public.users to anon;

grant delete, insert, references, select, trigger, truncate, update on public.users to authenticated;

grant delete, insert, references, select, trigger, truncate, update on public.users to service_role;

create table public.invitations
(
    id         uuid                     not null
        primary key,
    code       varchar(7)               not null
        constraint invitations_code_unique
            unique,
    used_by    uuid
        constraint invitations_used_by_fk
            references public.users
            on delete set null,
    created_at timestamp with time zone not null,
    expires_at timestamp with time zone not null,
    is_deleted boolean default false    not null,
    deleted_at timestamp with time zone,
    constraint invitations_expires_after_created
        check (expires_at > created_at)
);

alter table public.invitations
    owner to postgres;

create index ix_invitations_used_by
    on public.invitations (used_by);

create index ix_invitations_expires_at
    on public.invitations (expires_at);

grant delete, insert, references, select, trigger, truncate, update on public.invitations to anon;

grant delete, insert, references, select, trigger, truncate, update on public.invitations to authenticated;

grant delete, insert, references, select, trigger, truncate, update on public.invitations to service_role;

create table public.organisations
(
    id         uuid                     not null
        primary key,
    name       text                     not null,
    created_at timestamp with time zone not null,
    is_deleted boolean default false    not null,
    deleted_at timestamp with time zone
);

alter table public.organisations
    owner to postgres;

create unique index organisations_name_active_uniq
    on public.organisations (name)
    where (NOT is_deleted);

grant delete, insert, references, select, trigger, truncate, update on public.organisations to anon;

grant delete, insert, references, select, trigger, truncate, update on public.organisations to authenticated;

grant delete, insert, references, select, trigger, truncate, update on public.organisations to service_role;

create table public.memberships
(
    id              uuid                     not null
        primary key,
    user_id         uuid                     not null,
    organisation_id uuid                     not null
        references public.organisations
            on delete cascade,
    joined_at       timestamp with time zone not null,
    is_deleted      boolean  default false   not null,
    deleted_at      timestamp with time zone,
    status          smallint default 0       not null
        constraint memberships_status_range_chk
            check ((status >= 0) AND (status <= 2)),
    invited_by      uuid
        references public.users
);

alter table public.memberships
    owner to postgres;

create index idx_memberships_user_id
    on public.memberships (user_id);

create index idx_memberships_organisation_id
    on public.memberships (organisation_id);

create unique index memberships_user_org_active_uniq
    on public.memberships (user_id, organisation_id)
    where (NOT is_deleted);

create index idx_memberships_status
    on public.memberships (status);

create index idx_memberships_invited_by
    on public.memberships (invited_by);

grant delete, insert, references, select, trigger, truncate, update on public.memberships to anon;

grant delete, insert, references, select, trigger, truncate, update on public.memberships to authenticated;

grant delete, insert, references, select, trigger, truncate, update on public.memberships to service_role;

create table public.change_logs
(
    id              uuid                      not null
        primary key,
    organisation_id uuid                      not null
        references public.organisations
            on delete cascade,
    user_id         uuid                      not null,
    message         text                      not null,
    data            jsonb default '{}'::jsonb not null,
    created_at      timestamp with time zone  not null
);

alter table public.change_logs
    owner to postgres;

create index idx_change_log_organisation_id
    on public.change_logs (organisation_id);

create index idx_change_log_created_at
    on public.change_logs (created_at desc);

grant delete, insert, references, select, trigger, truncate, update on public.change_logs to anon;

grant delete, insert, references, select, trigger, truncate, update on public.change_logs to authenticated;

grant delete, insert, references, select, trigger, truncate, update on public.change_logs to service_role;

