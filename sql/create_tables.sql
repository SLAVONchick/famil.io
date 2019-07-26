create database familio;

create schema familio;

create table familio.familio.roles (
    id serial primary key,
    name varchar(128) not null
);

create table familio.familio.groups(
    id bigserial primary key,
    name varchar(128) not null,
    created_by varchar(512) not null,
    created_at timestamp not null,
    deleted_by varchar(512),
    deleted_at timestamp
);

create table familio.familio.users_roles_groups(
    user_id varchar(512) not null,
    role_id int not null references familio.familio.roles(id),
    group_id bigint not null references familio.familio.groups(id)
);

create table familio.familio.tasks(
    id uuid primary key,
    group_id bigint not null references familio.familio.groups(id),
    name varchar(256) not null,
    description text,
    created_by varchar(512) not null,
    created_at timestamp not null,
    executor varchar(512) not null,
    expires_by timestamp,
    status int not null,
    priority int not null
);

create table familio.familio.comments(
    id uuid not null,
    task_id uuid not null references familio.familio.tasks(id),
    user_id varchar(512) not null,
    contents varchar(300) not null,
    created_at timestamp not null,
    updated_at timestamp,
    constraint PK_comments primary key (id, task_id, user_id)
);